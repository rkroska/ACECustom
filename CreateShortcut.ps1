$WshShell = New-Object -ComObject WScript.Shell
$Desktop = [Environment]::GetFolderPath('Desktop')
$Shortcut = $WshShell.CreateShortcut($Desktop + '\ACE Server Manager.lnk')
$Shortcut.TargetPath = 'C:\Windows\System32\wscript.exe'
$Shortcut.Arguments = '"C:\ACE\ACEServerManager\Launch.vbs"'
$Shortcut.WorkingDirectory = 'C:\ACE\ACEServerManager'
$Shortcut.Description = 'ACE Server Manager - Start/Stop your ACE server'
$Shortcut.IconLocation = 'C:\Windows\System32\shell32.dll,162'
$Shortcut.Save()
Write-Host "Shortcut created on Desktop!"
