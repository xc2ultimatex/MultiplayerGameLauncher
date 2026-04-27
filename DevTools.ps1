Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

try {

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Game definitions ───────────────────────────────────────────────────────────
# Add new games here to have them appear in the dropdown automatically.
$Games = [ordered]@{
    "ShopGame"   = @{ DefaultBuildDir = "C:\UnityBuilds\ShopGame";   FeedDir = "C:\MultiplayerPrototypeBuilds\Latest" }
    "CamgirlSim" = @{ DefaultBuildDir = "C:\UnityBuilds\CamgirlSim"; FeedDir = "C:\MultiplayerPrototypeBuilds\CamgirlSim" }
}

# ── Form ───────────────────────────────────────────────────────────────────────
$form                 = New-Object Windows.Forms.Form
$form.Text            = "Dev Publisher"
$form.Size            = New-Object Drawing.Size(640, 800)
$form.MinimumSize     = New-Object Drawing.Size(640, 800)
$form.StartPosition   = "CenterScreen"
$form.BackColor       = [Drawing.Color]::FromArgb(245, 245, 245)
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox     = $false
$form.Font            = New-Object Drawing.Font("Segoe UI", 9)

# ── Builder helpers ────────────────────────────────────────────────────────────
function New-GroupBox($Text, $Top, $Height) {
    $gb           = New-Object Windows.Forms.GroupBox
    $gb.Text      = $Text
    $gb.Location  = New-Object Drawing.Point(12, $Top)
    $gb.Size      = New-Object Drawing.Size(600, $Height)
    $gb.Font      = New-Object Drawing.Font("Segoe UI", 9, [Drawing.FontStyle]::Bold)
    return $gb
}

function New-Lbl($Text, $X, $Y, $W = 80) {
    $l            = New-Object Windows.Forms.Label
    $l.Text       = $Text
    $l.Location   = New-Object Drawing.Point($X, $Y)
    $l.Size       = New-Object Drawing.Size($W, 22)
    $l.TextAlign  = "MiddleLeft"
    $l.Font       = New-Object Drawing.Font("Segoe UI", 9)
    return $l
}

function New-Btn($Text, $X, $Y, $W = 160, $H = 32) {
    $b                         = New-Object Windows.Forms.Button
    $b.Text                    = $Text
    $b.Location                = New-Object Drawing.Point($X, $Y)
    $b.Size                    = New-Object Drawing.Size($W, $H)
    $b.UseVisualStyleBackColor = $true
    $b.Font                    = New-Object Drawing.Font("Segoe UI", 9)
    return $b
}

function New-Txt($X, $Y, $W, $DefaultText = "") {
    $t          = New-Object Windows.Forms.TextBox
    $t.Location = New-Object Drawing.Point($X, $Y)
    $t.Size     = New-Object Drawing.Size($W, 24)
    $t.Text     = $DefaultText
    $t.Font     = New-Object Drawing.Font("Segoe UI", 9)
    return $t
}

# ── Log ────────────────────────────────────────────────────────────────────────
$script:logBox = $null

function Write-Log($Message, [switch]$Err, [switch]$Ok, [switch]$Head) {
    $color = if ($Err)       { [Drawing.Color]::FromArgb(255, 110, 110) }
             elseif ($Ok)   { [Drawing.Color]::FromArgb(100, 220, 120) }
             elseif ($Head) { [Drawing.Color]::FromArgb(100, 180, 255) }
             else           { [Drawing.Color]::FromArgb(210, 210, 210) }
    $script:logBox.SelectionStart  = $script:logBox.TextLength
    $script:logBox.SelectionLength = 0
    $script:logBox.SelectionColor  = $color
    $script:logBox.AppendText("$Message`n")
    $script:logBox.ScrollToCaret()
    [Windows.Forms.Application]::DoEvents()
}

function Run-Proc($Exe, $ArgStr, $WorkDir = $ScriptDir) {
    $psi                        = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName               = $Exe
    $psi.Arguments              = $ArgStr
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.CreateNoWindow         = $true
    $psi.WorkingDirectory       = $WorkDir
    $proc   = [Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput
    $stderr = $proc.StandardError

    # Peek-based non-blocking read so the UI stays live between output lines
    while (-not $proc.HasExited) {
        $gotLine = $false
        while ($stdout.Peek() -ge 0) {
            $line = $stdout.ReadLine()
            if ($line -and $line.Trim()) { Write-Log $line.Trim() }
            $gotLine = $true
        }
        [Windows.Forms.Application]::DoEvents()
        if (-not $gotLine) { Start-Sleep -Milliseconds 80 }
    }

    # Drain any remaining output after exit
    $rest = $stdout.ReadToEnd()
    if ($rest) { foreach ($line in ($rest -split "`n")) { if ($line.Trim()) { Write-Log $line.Trim() } } }

    $errText = $stderr.ReadToEnd()
    if ($errText) { foreach ($line in ($errText -split "`n")) { if ($line.Trim()) { Write-Log $line.Trim() -Err } } }

    return $proc.ExitCode
}

function Run-PS($ScriptPath, $ExtraArgs = "") {
    return Run-Proc "powershell.exe" "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" $ExtraArgs"
}

function Set-ButtonsEnabled($Enabled) {
    foreach ($sec in @($secSingle, $secAll, $secServer, $secLauncher)) {
        foreach ($ctrl in $sec.Controls) {
            if ($ctrl -is [Windows.Forms.Button]) { $ctrl.Enabled = $Enabled }
        }
    }
}

function Run-Operation($Header, [scriptblock]$Body) {
    Set-ButtonsEnabled $false
    Write-Log ""
    Write-Log "-- $Header --" -Head
    try   { & $Body }
    catch { Write-Log "Unexpected error: $($_.Exception.Message)" -Err }
    finally { Set-ButtonsEnabled $true }
}

# ══════════════════════════════════════════════════════════════════════════════
# Section 1 - Publish Single Game
# ══════════════════════════════════════════════════════════════════════════════
$secSingle = New-GroupBox "Publish Single Game" 10 148

$secSingle.Controls.Add((New-Lbl "Game:" 12 26 60))

$cboGame               = New-Object Windows.Forms.ComboBox
$cboGame.Location      = New-Object Drawing.Point(76, 24)
$cboGame.Size          = New-Object Drawing.Size(180, 24)
$cboGame.DropDownStyle = "DropDownList"
$cboGame.Font          = New-Object Drawing.Font("Segoe UI", 9)
foreach ($name in $Games.Keys) { [void]$cboGame.Items.Add($name) }
$cboGame.SelectedIndex = 0
$secSingle.Controls.Add($cboGame)

$secSingle.Controls.Add((New-Lbl "Build Dir:" 12 60 66))
$txtBuildDir = New-Txt 82 58 386 $Games[$cboGame.SelectedItem].DefaultBuildDir
$secSingle.Controls.Add($txtBuildDir)

$btnBrowse = New-Btn "Browse..." 476 56 106 28
$secSingle.Controls.Add($btnBrowse)

$btnPublishOne = New-Btn "Publish Selected Game" 12 98 200 34
$secSingle.Controls.Add($btnPublishOne)

$lblFeedTarget           = New-Lbl ("-> " + $Games[$cboGame.SelectedItem].FeedDir) 224 106 360
$lblFeedTarget.Font      = New-Object Drawing.Font("Consolas", 8)
$lblFeedTarget.ForeColor = [Drawing.Color]::Gray
$secSingle.Controls.Add($lblFeedTarget)

$cboGame.add_SelectedIndexChanged({
    $sel                = $cboGame.SelectedItem
    $txtBuildDir.Text   = $Games[$sel].DefaultBuildDir
    $lblFeedTarget.Text = "-> " + $Games[$sel].FeedDir
})

# ══════════════════════════════════════════════════════════════════════════════
# Section 2 - Publish All Games
# ══════════════════════════════════════════════════════════════════════════════
$secAll = New-GroupBox "Publish All Games" 166 66

$btnPublishAll = New-Btn "Publish All Games" 12 24 200 34
$secAll.Controls.Add($btnPublishAll)

$lblAllHint           = New-Lbl "Uses C:\UnityBuilds\<GameName> for each game." 224 32 360
$lblAllHint.Font      = New-Object Drawing.Font("Segoe UI", 8)
$lblAllHint.ForeColor = [Drawing.Color]::Gray
$secAll.Controls.Add($lblAllHint)

# ══════════════════════════════════════════════════════════════════════════════
# Section 3 - Update Server
# ══════════════════════════════════════════════════════════════════════════════
$script:serverPort = 8080

function Test-ServerRunning {
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async  = $client.BeginConnect("localhost", $script:serverPort, $null, $null)
        $ok     = $async.AsyncWaitHandle.WaitOne(400, $false)
        if ($ok) { try { $client.EndConnect($async) } catch {} }
        $client.Close()
        return $ok
    } catch {
        return $false
    }
}

function Update-ServerStatusLabel {
    if (Test-ServerRunning) {
        $lblServerStatus.Text      = "Server: RUNNING  (port $($script:serverPort))"
        $lblServerStatus.ForeColor = [Drawing.Color]::FromArgb(60, 180, 80)
        $btnStartServer.Enabled    = $false
        $btnStopServer.Enabled     = $true
    } else {
        $lblServerStatus.Text      = "Server: STOPPED"
        $lblServerStatus.ForeColor = [Drawing.Color]::FromArgb(210, 70, 70)
        $btnStartServer.Enabled    = $true
        $btnStopServer.Enabled     = $false
    }
}

$secServer = New-GroupBox "Update Server" 240 90

$btnCheckServer = New-Btn "Check Status" 12 24 130 30
$secServer.Controls.Add($btnCheckServer)

$btnStartServer = New-Btn "Start Server" 152 24 120 30
$secServer.Controls.Add($btnStartServer)

$btnStopServer          = New-Btn "Stop Server" 282 24 120 30
$btnStopServer.Enabled  = $false
$secServer.Controls.Add($btnStopServer)

$lblServerStatus           = New-Lbl "Server: Unknown" 12 62 560
$lblServerStatus.Font      = New-Object Drawing.Font("Segoe UI Semibold", 9, [Drawing.FontStyle]::Bold)
$lblServerStatus.ForeColor = [Drawing.Color]::Gray
$secServer.Controls.Add($lblServerStatus)

# Auto-check every 5 seconds
$statusTimer          = New-Object Windows.Forms.Timer
$statusTimer.Interval = 5000
$statusTimer.add_Tick({ Update-ServerStatusLabel })
$statusTimer.Start()

# ══════════════════════════════════════════════════════════════════════════════
# Section 4 - Publish Launcher to GitHub
# ══════════════════════════════════════════════════════════════════════════════
$secLauncher = New-GroupBox "Publish Launcher to GitHub" 338 126

$secLauncher.Controls.Add((New-Lbl "Version:" 12 28 64))
$txtVersion      = New-Txt 80 26 120 "1.0.0"
$secLauncher.Controls.Add($txtVersion)

$lblVerHint           = New-Lbl "(e.g. 1.0.1 - will tag as v1.0.1)" 210 30 360
$lblVerHint.Font      = New-Object Drawing.Font("Segoe UI", 8)
$lblVerHint.ForeColor = [Drawing.Color]::Gray
$secLauncher.Controls.Add($lblVerHint)

$secLauncher.Controls.Add((New-Lbl "Notes:" 12 62 64))
$txtNotes = New-Txt 80 60 502 "Launcher update"
$secLauncher.Controls.Add($txtNotes)

$btnPublishLauncher = New-Btn "Build and Publish to GitHub" 12 96 220 34
$secLauncher.Controls.Add($btnPublishLauncher)

$lblGhHint           = New-Lbl "Requires gh CLI installed and authenticated (gh auth login)." 244 104 340
$lblGhHint.Font      = New-Object Drawing.Font("Segoe UI", 8)
$lblGhHint.ForeColor = [Drawing.Color]::Gray
$secLauncher.Controls.Add($lblGhHint)

# ══════════════════════════════════════════════════════════════════════════════
# Log
# ══════════════════════════════════════════════════════════════════════════════
$lblLog = New-Lbl "Output:" 12 472 60

$btnClearLog          = New-Btn "Clear" 554 468 58 24
$btnClearLog.Font     = New-Object Drawing.Font("Segoe UI", 8)

$logBox              = New-Object Windows.Forms.RichTextBox
$logBox.Location     = New-Object Drawing.Point(12, 498)
$logBox.Size         = New-Object Drawing.Size(600, 230)
$logBox.ReadOnly     = $true
$logBox.BackColor    = [Drawing.Color]::FromArgb(22, 22, 28)
$logBox.ForeColor    = [Drawing.Color]::FromArgb(210, 210, 210)
$logBox.Font         = New-Object Drawing.Font("Consolas", 9)
$logBox.ScrollBars   = "Vertical"
$logBox.BorderStyle  = "FixedSingle"
$script:logBox       = $logBox

$form.Controls.AddRange(@($secSingle, $secAll, $secServer, $secLauncher, $lblLog, $btnClearLog, $logBox))

# ══════════════════════════════════════════════════════════════════════════════
# Button handlers
# ══════════════════════════════════════════════════════════════════════════════

$btnBrowse.add_Click({
    $dlg             = New-Object Windows.Forms.FolderBrowserDialog
    $dlg.Description = "Select the Unity build output folder"
    $dlg.SelectedPath = $txtBuildDir.Text
    if ($dlg.ShowDialog() -eq "OK") { $txtBuildDir.Text = $dlg.SelectedPath }
})

$btnPublishOne.add_Click({
    $game     = $cboGame.SelectedItem
    $buildDir = $txtBuildDir.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($buildDir) -or -not (Test-Path $buildDir)) {
        [Windows.Forms.MessageBox]::Show("Build directory not found:`n$buildDir", "Error")
        return
    }
    Run-Operation "Publishing $game" {
        $code = Run-PS (Join-Path $ScriptDir "Publish-LatestBuild.ps1") "-GameName `"$game`" -SourceBuildDirectory `"$buildDir`""
        if ($code -eq 0) { Write-Log "$game published successfully." -Ok }
        else             { Write-Log "$game publish failed (exit $code)." -Err }
    }
})

$btnPublishAll.add_Click({
    Run-Operation "Publishing All Games" {
        $code = Run-PS (Join-Path $ScriptDir "Publish-AllBuilds.ps1")
        if ($code -eq 0) { Write-Log "All games published." -Ok }
        else             { Write-Log "One or more games failed." -Err }
    }
})

$btnCheckServer.add_Click({
    Update-ServerStatusLabel
    Write-Log "Server status: $($lblServerStatus.Text)"
})

$btnStartServer.add_Click({
    $serverScript = Join-Path $ScriptDir "Start-UpdateFeedServer.ps1"
    Write-Log ""
    Write-Log "-- Starting Update Server --" -Head
    # UseShellExecute=true detaches the new window so this one does not freeze
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName        = "powershell.exe"
    $psi.Arguments       = "-NoProfile -ExecutionPolicy Bypass -File `"$serverScript`""
    $psi.UseShellExecute = $true
    $psi.WindowStyle     = [Diagnostics.ProcessWindowStyle]::Normal
    [void][Diagnostics.Process]::Start($psi)
    Write-Log "Waiting for server to bind to port $($script:serverPort)..."
    $started = $false
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        [Windows.Forms.Application]::DoEvents()
        if (Test-ServerRunning) { $started = $true; break }
    }
    Update-ServerStatusLabel
    if ($started) { Write-Log "Server is running on port $($script:serverPort)." -Ok }
    else          { Write-Log "Server did not respond after 10s. Check the server window for errors." -Err }
})

$btnStopServer.add_Click({
    Write-Log ""
    Write-Log "-- Stopping Update Server --" -Head
    if (-not (Test-ServerRunning)) {
        Write-Log "No server is running on port $($script:serverPort)."
    } else {
        $listeners = Get-NetTCPConnection -LocalPort $script:serverPort -State Listen -ErrorAction SilentlyContinue
        if ($listeners) {
            foreach ($l in $listeners) {
                try { Stop-Process -Id $l.OwningProcess -Force -ErrorAction Stop; Write-Log "Stopped PID $($l.OwningProcess)." -Ok }
                catch { Write-Log "Could not stop PID $($l.OwningProcess): $($_.Exception.Message)" -Err }
            }
        }
    }
    Start-Sleep -Milliseconds 500
    Update-ServerStatusLabel
})

$btnPublishLauncher.add_Click({
    $version = $txtVersion.Text.Trim()
    $notes   = $txtNotes.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        [Windows.Forms.MessageBox]::Show("Enter a version number.", "Error")
        return
    }
    Run-Operation "Build and Publish Launcher v$version" {
        Write-Log "Building launcher..."
        $proj   = Join-Path $ScriptDir "MultiplayerLauncher.csproj"
        $outDir = Join-Path $ScriptDir "dist"
        $psi = New-Object Diagnostics.ProcessStartInfo
        $psi.FileName               = "dotnet"
        $psi.Arguments              = "publish `"$proj`" -c Release -o `"$outDir`" --nologo"
        $psi.UseShellExecute        = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        $psi.CreateNoWindow         = $true
        $psi.WorkingDirectory       = $ScriptDir
        $buildProc = [Diagnostics.Process]::Start($psi)
        while (-not $buildProc.StandardOutput.EndOfStream) {
            $line = $buildProc.StandardOutput.ReadLine()
            if ($line -and $line.Trim()) { Write-Log $line.Trim() }
            [Windows.Forms.Application]::DoEvents()
        }
        $buildErr = $buildProc.StandardError.ReadToEnd()
        $buildProc.WaitForExit()
        if ($buildErr) { foreach ($line in ($buildErr -split "`n")) { if ($line.Trim()) { Write-Log $line.Trim() -Err } } }
        $code = $buildProc.ExitCode
        if ($code -ne 0) { Write-Log "Build failed." -Err; return }
        Write-Log "Build succeeded." -Ok

        $exe = Join-Path $outDir "MultiplayerLauncher.exe"
        if (-not (Test-Path $exe)) { Write-Log "Exe not found: $exe" -Err; return }

        Write-Log "Creating GitHub release v$version..."
        $tag  = "v$version"
        $code2 = Run-Proc "gh" "release create `"$tag`" `"$exe`" --title `"$tag`" --notes `"$notes`"" $ScriptDir
        if ($code2 -eq 0) { Write-Log "Launcher $tag published to GitHub." -Ok }
        else              { Write-Log "GitHub release failed (exit $code2). Is gh installed and authenticated?" -Err }
    }
})

$btnClearLog.add_Click({ $logBox.Clear() })

# ── Launch ─────────────────────────────────────────────────────────────────────
Update-ServerStatusLabel
Write-Log "Dev Publisher ready." -Ok
Write-Log "Select a game and build directory, then hit Publish."

[void]$form.ShowDialog()

} catch {
    [System.Windows.Forms.MessageBox]::Show(
        "DevTools failed to start:`n`n$($_.Exception.Message)`n`nLine: $($_.InvocationInfo.ScriptLineNumber)",
        "DevTools Error",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    )
}
