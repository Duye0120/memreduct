function Assert-BuildOutputsAvailable {
    param([Parameter(Mandatory)][string[]]$Directories)
    $roots = @($Directories | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') + '\' })
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='MemReduct.Fluent.exe'" -ErrorAction Stop)) {
        # Elevation can hide a running path. In that case we cannot safely rule out a locked output.
        if (!$process.ExecutablePath) { throw 'Exit Mem Reduct from its tray menu: a running instance has an inaccessible path.' }
        foreach ($root in $roots) {
            if ($process.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Exit the Mem Reduct instance using this build output: $($process.ExecutablePath)"
            }
        }
    }
}
