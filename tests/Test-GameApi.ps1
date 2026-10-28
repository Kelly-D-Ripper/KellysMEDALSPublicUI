param(
    [string]$GameDir = $(if ($env:NUCLEAR_OPTION_DIR) { $env:NUCLEAR_OPTION_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option' }),
    [string]$ClientDll = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin/Release/KellysMEDALSPublicUI.dll'),
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'NuclearOption_Data/Managed'
$core = Join-Path $GameDir 'BepInEx/core'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')

function Assert-GameContract($game) {
    $chat = $game.MainModule.Types | Where-Object FullName -eq 'NuclearOption.Chat.ChatManager'
    foreach ($contract in @(
        @('SendChatMessage','System.String,System.Boolean','System.Void'),
        @('CanSend','System.String,System.Boolean,System.Boolean','System.Boolean'),
        @('UserCode_CmdSendChatMessage_-456754112','System.String,System.Boolean,Mirage.INetworkPlayer','System.Void'),
        @('Skeleton_CmdSendChatMessage_-456754112','Mirage.NetworkBehaviour,Mirage.Serialization.NetworkReader,Mirage.INetworkPlayer,System.Int32','System.Void'),
        @('UserCode_RpcTargetServerMessage_-537879009','Mirage.INetworkPlayer,System.String,System.Boolean','System.Void')
    )) {
        $method = @($chat.Methods | Where-Object Name -eq $contract[0])
        if ($method.Count -ne 1 -or (($method[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ne $contract[1] -or
            $method[0].ReturnType.FullName -ne $contract[2]) { throw "Native chat contract changed: $($contract[0])" }
    }
    $send = $chat.Methods | Where-Object Name -eq 'SendChatMessage'
    $calls = @($send.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.FullName })
    $expected = @('System.Boolean NuclearOption.Chat.ChatManager::CanSend(System.String,System.Boolean,System.Boolean)',
        'System.Void NuclearOption.Chat.ChatManager::CmdSendChatMessage(System.String,System.Boolean,Mirage.INetworkPlayer)')
    if ($calls.Count -ne 2 -or @(Compare-Object $calls $expected).Count -ne 0) {
        throw 'Direct MEDALS request route changed; inspect the new parser before releasing.'
    }
    $mfd = $game.MainModule.Types | Where-Object FullName -eq 'VirtualMFD'
    foreach ($contract in @(
        @('leftScreens','System.Collections.Generic.List`1<MFDScreen>'),
        @('leftButtons','System.Collections.Generic.List`1<UnityEngine.UI.Button>'),
        @('activeLeft','MFDScreen')
    )) {
        $field = @($mfd.Fields | Where-Object Name -eq $contract[0])
        if ($field.Count -ne 1 -or $field[0].FieldType.FullName -ne $contract[1]) { throw "Native MFD contract changed: $($contract[0])" }
    }
}

$gamePath = Join-Path $managed 'Assembly-CSharp.dll'
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gamePath)
try {
    Assert-GameContract $game
    Write-Host 'PASS: native chat signatures, direct request route and MFD fields.'
    if ($SelfTest) {
        # Fault injection changes only in-memory copies; never the installed game files.
        foreach ($fault in @('missing receiver','changed signature','changed route','changed MFD')) {
            $broken = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gamePath)
            try {
                $chat = $broken.MainModule.Types | Where-Object FullName -eq 'NuclearOption.Chat.ChatManager'
                $receive = $chat.Methods | Where-Object Name -eq 'UserCode_RpcTargetServerMessage_-537879009'
                switch ($fault) {
                    'missing receiver' { $receive.Name = 'ChangedReceiver' }
                    'changed signature' { $receive.Parameters[1].ParameterType = $broken.MainModule.TypeSystem.Int32 }
                    'changed route' {
                        $send = $chat.Methods | Where-Object Name -eq 'SendChatMessage'
                        $call = $send.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'CmdSendChatMessage' }
                        $call.Operand = $receive
                    }
                    'changed MFD' {
                        $mfd = $broken.MainModule.Types | Where-Object FullName -eq 'VirtualMFD'
                        ($mfd.Fields | Where-Object Name -eq 'leftScreens').Name = 'ChangedScreens'
                    }
                }
                $rejected = $false
                try { Assert-GameContract $broken } catch { $rejected = $true }
                if (!$rejected) { throw "Compatibility audit accepted fault: $fault" }
                Write-Host "PASS: rejects $fault"
            } finally { $broken.Dispose() }
        }
    }
} finally { $game.Dispose() }

$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($managed)
$resolver.AddSearchDirectory($core)
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$client = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $ClientDll).Path, $parameters)
try {
    $plugin = $client.MainModule.Types | Where-Object FullName -eq 'KellysMEDALSPublicUI.Plugin'
    $manifest = $plugin.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' }
    $values = @($manifest.ConstructorArguments | ForEach-Object Value)
    if ($values.Count -ne 3 -or $values[0] -ne 'kelly.nuclearoption.medals.publicui' -or $values[2] -ne '0.1.2') { throw 'Unexpected client plugin metadata.' }
    $refs = @($client.MainModule.GetMemberReferences() | Where-Object { $_.DeclaringType.Scope.Name -match '^(Assembly-CSharp|Mirage|UnityEngine.*|Unity.TextMeshPro)$' })
    foreach ($reference in $refs) {
        if ($null -eq $reference.Resolve()) { throw "Unresolved game member: $($reference.FullName)" }
    }
    $update = $plugin.Methods | Where-Object Name -eq 'Update'
    $sends = @($update.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'NuclearOption.Chat.ChatManager' -and $_.Operand.Name -eq 'SendChatMessage' })
    if ($sends.Count -ne 1) { throw 'Client no longer uses the audited direct request route.' }
    Write-Host "PASS: client 0.1.2 metadata, request call and $($refs.Count) game/Unity/Mirage member bindings."
    Write-Host "Assembly-CSharp SHA256: $((Get-FileHash -LiteralPath $gamePath).Hash)"
} finally { $client.Dispose(); $resolver.Dispose() }
