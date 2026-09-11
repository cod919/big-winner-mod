Set sh = CreateObject("WScript.Shell")
Set fs = CreateObject("Scripting.FileSystemObject")
root = fs.GetParentFolderName(WScript.ScriptFullName)
launchCommand = "powershell.exe -NoProfile -WindowStyle Hidden -File " & Chr(34) & fs.BuildPath(root, "Start-Mod.ps1") & Chr(34)
If WScript.Arguments.Named.Exists("check") Then
    WScript.Echo launchCommand
Else
    sh.Run launchCommand, 0, False
End If
