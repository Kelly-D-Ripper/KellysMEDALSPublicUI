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
        ,@('UserCode_RpcTargetServerMessage_-537879009','Mirage.INetworkPlayer,System.String,System.Boolean','System.Void')
    )) {
        $method = @($chat.Methods | Where-Object Name -eq $contract[0])
        if ($method.Count -ne 1 -or (($method[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ne $contract[1] -or
            $method[0].ReturnType.FullName -ne $contract[2]) { throw "Native chat contract changed: $($contract[0])" }
    }
    $commands = $game.MainModule.Types | Where-Object Name -eq 'ChatCommandManager'
    foreach ($contract in @(
        @('ExecuteCommandLocal','System.String,System.String','System.Void'),
        @('TryGetModServerCommandDescriptor','System.String,NuclearOption.Chat.ChatCommandDescriptor&','System.Boolean'),
        @('CmdExecuteCommand','System.String,System.String,Mirage.INetworkPlayer','Cysharp.Threading.Tasks.UniTask`1<NuclearOption.ModdingApi.ChatCommandResult>')
    )) {
        $method = @($commands.Methods | Where-Object Name -eq $contract[0])
        if ($method.Count -ne 1 -or (($method[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ne $contract[1] -or
            $method[0].ReturnType.FullName -ne $contract[2]) { throw "Native command contract changed: $($contract[0])" }
    }
    foreach ($route in @(@('ExecuteCommandLocal','SendServerCommand'), @('SendServerCommand','SendServerCommandAsync'))) {
        $method = $commands.Methods | Where-Object Name -eq $route[0]
        if (!@($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $route[1] }).Count) {
            throw "Native command route changed: $($route[0])"
        }
    }
    $async = $commands.NestedTypes | Where-Object Name -like '<SendServerCommandAsync>*'
    if (!@($async.Methods.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'CmdExecuteCommand' }).Count) {
        throw 'Command transport no longer invokes the command RPC.'
    }
    $allMethods = @($commands.Methods) + @($commands.NestedTypes | ForEach-Object { $_.Methods })
    foreach ($method in $allMethods) {
        if (@($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'ChatManager' }).Count) {
            throw 'Command manager now calls ChatManager; privacy route must be reviewed.'
        }
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
    Write-Host 'PASS: native private receiver, capability lookup, command RPC route without ChatManager calls, and MFD fields.'
    if ($SelfTest) {
        # Fault injection changes only in-memory copies; never the installed game files.
        foreach ($fault in @('missing receiver','changed signature','changed route','chat fallback','changed MFD')) {
            $broken = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gamePath)
            try {
                $chat = $broken.MainModule.Types | Where-Object FullName -eq 'NuclearOption.Chat.ChatManager'
                $receive = $chat.Methods | Where-Object Name -eq 'UserCode_RpcTargetServerMessage_-537879009'
                switch ($fault) {
                    'missing receiver' { $receive.Name = 'ChangedReceiver' }
                    'changed signature' { $receive.Parameters[1].ParameterType = $broken.MainModule.TypeSystem.Int32 }
                    'changed route' {
                        $commands = $broken.MainModule.Types | Where-Object Name -eq 'ChatCommandManager'
                        $send = $commands.Methods | Where-Object Name -eq 'ExecuteCommandLocal'
                        $call = $send.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'SendServerCommand' } | Select-Object -First 1
                        $call.Operand = $receive
                    }
                    'chat fallback' {
                        $commands = $broken.MainModule.Types | Where-Object Name -eq 'ChatCommandManager'
                        $send = $commands.Methods | Where-Object Name -eq 'SendServerCommand'
                        $call = $send.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'ShowResult' } | Select-Object -First 1
                        $call.Operand = ($chat.Methods | Where-Object Name -eq 'SendChatMessage')
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
    if ($values.Count -ne 3 -or $values[0] -ne 'kelly.nuclearoption.medals.publicui' -or $values[2] -ne '0.1.4') { throw 'Unexpected client plugin metadata.' }
    $resources = @($client.MainModule.Resources | Where-Object Name -eq 'doorman.json')
    if ($resources.Count -ne 1 -or $resources[0] -isnot [Mono.Cecil.EmbeddedResource]) { throw 'Expected one embedded DOORMAN manifest.' }
    $stream = $resources[0].GetResourceStream()
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8)
    try { $card = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose(); $stream.Dispose() }
    $expected = 'Track your career on the Aryx Shipyard Server with a MEDALS panel on the enlarged map. Browse earned medals, upcoming awards, progress and full requirements; filter by category and PvP, PvE or Universal track.'
    if ($card.plugin -cne $values[0] -or $card.name -cne $values[1] -or $card.author -cne 'Brig. Gen. Kelly D. Ripper' -or $card.description -cne $expected -or $null -ne $card.image -or
        @($card.links | Where-Object { $_.label -eq 'GitHub' -and $_.url -ceq 'https://github.com/Kelly-D-Ripper/KellysMEDALSPublicUI' }).Count -ne 1 -or
        @($card.links | Where-Object { $_.label -eq 'Discord' -and $_.url -ceq 'https://discord.gg/shipyard' }).Count -ne 1 -or
        $card.update.repository -cne 'Kelly-D-Ripper/KellysMEDALSPublicUI' -or $card.update.asset -cne 'KellysMEDALSPublicUI-*-CLIENT.zip') { throw 'Unexpected embedded DOORMAN metadata.' }
    $refs = @($client.MainModule.GetMemberReferences() | Where-Object { $_.DeclaringType.Scope.Name -match '^(Assembly-CSharp|Mirage|UnityEngine.*|Unity.TextMeshPro)$' })
    foreach ($reference in $refs) {
        if ($null -eq $reference.Resolve()) { throw "Unresolved game member: $($reference.FullName)" }
    }
    $update = $plugin.Methods | Where-Object Name -eq 'Update'
    $sends = @($update.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'NuclearOption.Chat.ChatCommandManager' -and $_.Operand.Name -eq 'ExecuteCommandLocal' })
    if ($sends.Count -ne 1) { throw 'Client no longer uses the audited native command route.' }
    if (@($client.MainModule.GetMemberReferences() | Where-Object { $_.Name -match 'SendChatMessage|CanSend' }).Count) { throw 'Client contains an ordinary chat send reference.' }
    Write-Host "PASS: client 0.1.4 metadata and DOORMAN entry, native-only request call and $($refs.Count) game/Unity/Mirage member bindings."
    Write-Host "Assembly-CSharp SHA256: $((Get-FileHash -LiteralPath $gamePath).Hash)"
} finally { $client.Dispose(); $resolver.Dispose() }
