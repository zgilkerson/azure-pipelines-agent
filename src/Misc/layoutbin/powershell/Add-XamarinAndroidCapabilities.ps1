[CmdletBinding()]
param()
$vs15 = Get-VisualStudio_15_0
if ($vs15 -and $vs15.installationPath) {
    # End with "\" for consistency with old ShellFolder values.
    $shellFolder15 = $vs15.installationPath.TrimEnd('\'[0]) + "\"
    Write-Capability -Name 'VisualStudio_15.0' -Value $shellFolder15
    $latestVS = $shellFolder15
    $installDir15 = [System.IO.Path]::Combine($shellFolder15, 'Common7\IDE\ReferenceAssembiles\Microsoft\Framework')
    [Version]$latestVersion = "0.0.0.0"
    
    # check for mono.android.jar under the vs2017 installation folder 
    try {
        $androidFiles = Get-ChildItem $installDir15 -Name 'mono.android.jar' -Recurse
        foreach ($androidFile in $androidFiles) {
            [Version]$version = $androidFile.TrimStart('MonoAndroid').TrimEnd('mono.android.jar').Trim('\').TrimStart('v')
            if ($version -gt $latestVersion) {
                $latestVersion = $version
            }
        }
    } catch {
        Write-Host ($_ | Out-String)
    }
    if ($latestVersion -gt [Version]"0.0.0.0") {
        Write-Capability -Name 'Xamarin.Android' -Value $latestVersion
    }
}
else {
    $null = Add-CapabilityFromRegistry -Name 'Xamarin.Android' -Hive 'LocalMachine' -View 'Registry32' -KeyName 'Software\Novell\Mono for Android' -ValueName 'InstalledVersion'
}