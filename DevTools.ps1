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
$form.Size            = New-Object Drawing.Size(640, 942)
$form.MinimumSize     = New-Object Drawing.Size(640, 842)
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
    $psi.RedirectStandardInput  = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.CreateNoWindow         = $true
    $psi.WorkingDirectory       = $WorkDir
    $proc   = [Diagnostics.Process]::Start($psi)
    $proc.StandardInput.Close()   # prevent interactive prompts from blocking
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
    foreach ($sec in @($secSingle, $secAll, $secServer, $secBroker, $secLauncher)) {
        foreach ($ctrl in $sec.Controls) {
            if ($ctrl -is [Windows.Forms.Button]) { $ctrl.Enabled = $Enabled }
        }
    }
    # Re-apply per-broker enabled state after bulk toggle
    if ($Enabled) { Update-AllBrokerStatuses }
    # Build buttons are always enabled (they handle stop+build+start themselves)
    if ($Enabled) {
        foreach ($b in $script:Brokers.Values) {
            if ($b.BuildBtn -ne $null) { $b.BuildBtn.Enabled = $true }
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
# Section 4 - Broker Management
# ══════════════════════════════════════════════════════════════════════════════

# Each entry: Name, path textbox, start/stop/build buttons, status label, running process
# BuildCsproj/BuildWorkDir: set to rebuild before (re)starting; leave empty to skip build step
$script:Brokers = [ordered]@{
    "ShopGame Broker" = @{
        Path         = "C:\Users\Corey\Documents\MultiplayerPrototype\BuildTools\BrokerServer\bin\Release\net8.0\MultiplayerPrototype.BrokerServer.exe"
        BuildCsproj  = "C:\Users\Corey\Documents\MultiplayerPrototype\BuildTools\BrokerServer\MultiplayerPrototype.BrokerServer.csproj"
        BuildWorkDir = "C:\Users\Corey\Documents\MultiplayerPrototype"
        Port = 0; Proc = $null; PathBox = $null; StartBtn = $null; StopBtn = $null; BuildBtn = $null; StatusLbl = $null
    }
    "Launcher Broker" = @{
        Path         = "C:\MultiplayerLauncher\LauncherBroker\bin\Release\net8.0\MultiplayerLauncher.LauncherBroker.exe"
        BuildCsproj  = ""
        BuildWorkDir = ""
        Port = 0; Proc = $null; PathBox = $null; StartBtn = $null; StopBtn = $null; BuildBtn = $null; StatusLbl = $null
    }
}

function Rebuild-Broker($Name) {
    $b = $script:Brokers[$Name]
    if ([string]::IsNullOrWhiteSpace($b.BuildCsproj)) {
        Write-Log "No build command configured for $Name." -Err; return
    }

    # Stop if running
    if ($b.Proc -and -not $b.Proc.HasExited) {
        try { $b.Proc.Kill(); Write-Log "Stopped $Name for rebuild." }
        catch { Write-Log "Could not stop ${Name}: $($_.Exception.Message)" -Err }
        $b.Proc = $null
        Update-BrokerStatus $Name
        Start-Sleep -Milliseconds 800
        [Windows.Forms.Application]::DoEvents()
    }

    # Build
    Write-Log "Building $Name..." -Head
    $code = Run-Proc "dotnet" "build `"$($b.BuildCsproj)`" -c Release --nologo" $b.BuildWorkDir
    if ($code -ne 0) {
        Write-Log "Build failed (exit $code). Broker not restarted." -Err
        Update-BrokerStatus $Name
        return
    }
    Write-Log "Build succeeded." -Ok

    # Restart
    Start-Broker $Name
}

function Start-Broker($Name) {
    $b = $script:Brokers[$Name]
    $path = $b.PathBox.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path $path)) {
        Write-Log "Broker path not found: $path" -Err; return
    }
    Write-Log "Starting $Name..." -Head
    $ext = [System.IO.Path]::GetExtension($path).ToLower()
    $psi = New-Object Diagnostics.ProcessStartInfo
    switch ($ext) {
        ".js"  { $psi.FileName = "node"; $psi.Arguments = "`"$path`"" }
        ".py"  { $psi.FileName = "python"; $psi.Arguments = "`"$path`"" }
        ".ps1" { $psi.FileName = "powershell.exe"; $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$path`"" }
        default { $psi.FileName = $path }
    }
    $psi.UseShellExecute = $true
    $psi.WindowStyle     = [Diagnostics.ProcessWindowStyle]::Normal
    try {
        $b.Proc = [Diagnostics.Process]::Start($psi)
        Write-Log "$Name started (PID $($b.Proc.Id))." -Ok
    } catch {
        Write-Log "Failed to start ${Name}: $($_.Exception.Message)" -Err
    }
    Update-BrokerStatus $Name
}

function Stop-Broker($Name) {
    $b = $script:Brokers[$Name]
    if ($b.Proc -and -not $b.Proc.HasExited) {
        try { $b.Proc.Kill(); Write-Log "$Name stopped." -Ok }
        catch { Write-Log "Could not stop ${Name}: $($_.Exception.Message)" -Err }
        $b.Proc = $null
    } else {
        Write-Log "$Name is not running."
    }
    Update-BrokerStatus $Name
}

function Test-BrokerRunning($Name) {
    $b = $script:Brokers[$Name]
    if ($b.Proc -eq $null) { return $false }
    try { return -not $b.Proc.HasExited } catch { return $false }
}

function Update-BrokerStatus($Name) {
    $b = $script:Brokers[$Name]
    $running = Test-BrokerRunning $Name
    if ($running) {
        $b.StatusLbl.Text      = "RUNNING"
        $b.StatusLbl.ForeColor = [Drawing.Color]::FromArgb(60,180,80)
        $b.StartBtn.Enabled    = $false
        $b.StopBtn.Enabled     = $true
    } else {
        $b.StatusLbl.Text      = "STOPPED"
        $b.StatusLbl.ForeColor = [Drawing.Color]::FromArgb(210,70,70)
        $b.StartBtn.Enabled    = $true
        $b.StopBtn.Enabled     = $false
        $b.Proc                = $null
    }
}

function Update-AllBrokerStatuses {
    foreach ($name in $script:Brokers.Keys) { Update-BrokerStatus $name }
}

$secBroker = New-GroupBox "Broker Management" 338 152

$hintLbl           = New-Lbl "Path to broker script or exe.  Node (.js)  Python (.py)  PowerShell (.ps1)  .exe" 12 18 580
$hintLbl.Font      = New-Object Drawing.Font("Segoe UI", 8)
$hintLbl.ForeColor = [Drawing.Color]::Gray
$secBroker.Controls.Add($hintLbl)

$brokerRow = 36
foreach ($bName in $script:Brokers.Keys) {
    $b        = $script:Brokers[$bName]
    $hasBuild = -not [string]::IsNullOrWhiteSpace($b.BuildCsproj)

    # Layout: compressed when a Build button is present, normal otherwise
    if ($hasBuild) {
        $lblW = 108; $pathX = 124; $pathW = 184; $browseX = 312; $browseW = 34
        $buildX = 350; $startX = 412; $stopX = 478; $btnW = 60; $statusX = 542
    } else {
        $lblW = 120; $pathX = 136; $pathW = 244; $browseX = 384; $browseW = 44
        $buildX = -1; $startX = 432; $stopX = 508; $btnW = 72; $statusX = 584
    }

    $lbl = New-Lbl "${bName}:" 12 ($brokerRow + 5) $lblW
    $secBroker.Controls.Add($lbl)

    $pathBox   = New-Txt $pathX $brokerRow $pathW $b.Path
    $b.PathBox = $pathBox
    $secBroker.Controls.Add($pathBox)

    $browseBtn     = New-Btn "..." $browseX ($brokerRow - 1) $browseW 28
    $browseBtn.Tag = $bName
    $browseBtn.add_Click({
        $n   = $this.Tag
        $dlg = New-Object Windows.Forms.OpenFileDialog
        $dlg.Filter = "Broker files (*.js;*.py;*.ps1;*.exe)|*.js;*.py;*.ps1;*.exe|All files (*.*)|*.*"
        $dlg.Title  = "Select broker for $n"
        if ($dlg.ShowDialog() -eq "OK") {
            $script:Brokers[$n].PathBox.Text = $dlg.FileName
        }
    })
    $secBroker.Controls.Add($browseBtn)

    # Build & Restart button — only for brokers with a build command
    if ($hasBuild) {
        $buildBtn                         = New-Btn "Build" $buildX ($brokerRow - 1) 58 28
        $buildBtn.BackColor               = [Drawing.Color]::FromArgb(140, 90, 20)
        $buildBtn.ForeColor               = [Drawing.Color]::White
        $buildBtn.UseVisualStyleBackColor = $false
        $buildBtn.Tag                     = $bName
        $buildBtn.add_Click({
            $n = $this.Tag
            Write-Log ""
            Write-Log "-- Rebuild & Restart $n --" -Head
            Rebuild-Broker $n
        })
        $b.BuildBtn = $buildBtn
        $secBroker.Controls.Add($buildBtn)
    }

    $startBtn                         = New-Btn "Start" $startX ($brokerRow - 1) $btnW 28
    $startBtn.BackColor               = [Drawing.Color]::FromArgb(40,100,50)
    $startBtn.ForeColor               = [Drawing.Color]::White
    $startBtn.UseVisualStyleBackColor = $false
    $startBtn.Tag                     = $bName
    $startBtn.add_Click({
        $n = $this.Tag
        Write-Log ""
        Write-Log "-- Starting $n --" -Head
        Start-Broker $n
    })
    $b.StartBtn = $startBtn
    $secBroker.Controls.Add($startBtn)

    $stopBtn                         = New-Btn "Stop" $stopX ($brokerRow - 1) $btnW 28
    $stopBtn.BackColor               = [Drawing.Color]::FromArgb(100,40,40)
    $stopBtn.ForeColor               = [Drawing.Color]::White
    $stopBtn.UseVisualStyleBackColor = $false
    $stopBtn.Enabled                 = $false
    $stopBtn.Tag                     = $bName
    $stopBtn.add_Click({
        $n = $this.Tag
        Write-Log ""
        Write-Log "-- Stopping $n --" -Head
        Stop-Broker $n
    })
    $b.StopBtn = $stopBtn
    $secBroker.Controls.Add($stopBtn)

    $statusLbl           = New-Lbl "STOPPED" $statusX ($brokerRow + 5) 58
    $statusLbl.ForeColor = [Drawing.Color]::FromArgb(210,70,70)
    $statusLbl.Font      = New-Object Drawing.Font("Segoe UI Semibold", 8, [Drawing.FontStyle]::Bold)
    $b.StatusLbl         = $statusLbl
    $secBroker.Controls.Add($statusLbl)

    $brokerRow += 40
}

# Refresh broker status every 3 seconds
$brokerTimer          = New-Object Windows.Forms.Timer
$brokerTimer.Interval = 3000
$brokerTimer.add_Tick({ Update-AllBrokerStatuses })
$brokerTimer.Start()

# ══════════════════════════════════════════════════════════════════════════════
# Section 5 - Publish Launcher to GitHub
# ══════════════════════════════════════════════════════════════════════════════
$secLauncher = New-GroupBox "Publish Launcher to GitHub" 498 168

$btnBuildDist                         = New-Btn "Build to dist" 12 24 140 30
$btnBuildDist.BackColor               = [Drawing.Color]::FromArgb(35, 80, 140)
$btnBuildDist.ForeColor               = [Drawing.Color]::White
$btnBuildDist.UseVisualStyleBackColor = $false
$secLauncher.Controls.Add($btnBuildDist)

$lblBuildHint           = New-Lbl "dotnet publish -c Release -o dist" 160 30 400
$lblBuildHint.Font      = New-Object Drawing.Font("Consolas", 8)
$lblBuildHint.ForeColor = [Drawing.Color]::Gray
$secLauncher.Controls.Add($lblBuildHint)

$sepLine           = New-Object Windows.Forms.Panel
$sepLine.Location  = New-Object Drawing.Point(12, 62)
$sepLine.Size      = New-Object Drawing.Size(572, 1)
$sepLine.BackColor = [Drawing.Color]::FromArgb(200, 200, 200)
$secLauncher.Controls.Add($sepLine)

$secLauncher.Controls.Add((New-Lbl "Version:" 12 74 64))
$txtVersion      = New-Txt 80 72 120 "1.0.0"
$secLauncher.Controls.Add($txtVersion)

$lblVerHint           = New-Lbl "(e.g. 1.0.1 - will tag as v1.0.1)" 210 76 360
$lblVerHint.Font      = New-Object Drawing.Font("Segoe UI", 8)
$lblVerHint.ForeColor = [Drawing.Color]::Gray
$secLauncher.Controls.Add($lblVerHint)

$secLauncher.Controls.Add((New-Lbl "Notes:" 12 106 64))
$txtNotes = New-Txt 80 104 502 "Launcher update"
$secLauncher.Controls.Add($txtNotes)

$btnPublishLauncher = New-Btn "Build and Publish to GitHub" 12 136 220 34
$secLauncher.Controls.Add($btnPublishLauncher)

$lblGhHint           = New-Lbl "Requires gh CLI installed and authenticated (gh auth login)." 244 144 340
$lblGhHint.Font      = New-Object Drawing.Font("Segoe UI", 8)
$lblGhHint.ForeColor = [Drawing.Color]::Gray
$secLauncher.Controls.Add($lblGhHint)

# ══════════════════════════════════════════════════════════════════════════════
# Log
# ══════════════════════════════════════════════════════════════════════════════
$lblLog = New-Lbl "Output:" 12 674 60

$btnClearLog          = New-Btn "Clear" 554 670 58 24
$btnClearLog.Font     = New-Object Drawing.Font("Segoe UI", 8)

$logBox              = New-Object Windows.Forms.RichTextBox
$logBox.Location     = New-Object Drawing.Point(12, 700)
$logBox.Size         = New-Object Drawing.Size(600, 200)
$logBox.ReadOnly     = $true
$logBox.BackColor    = [Drawing.Color]::FromArgb(22, 22, 28)
$logBox.ForeColor    = [Drawing.Color]::FromArgb(210, 210, 210)
$logBox.Font         = New-Object Drawing.Font("Consolas", 9)
$logBox.ScrollBars   = "Vertical"
$logBox.BorderStyle  = "FixedSingle"
$script:logBox       = $logBox

$form.Controls.AddRange(@($secSingle, $secAll, $secServer, $secBroker, $secLauncher, $lblLog, $btnClearLog, $logBox))

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

$btnBuildDist.add_Click({
    Run-Operation "Build Launcher to dist" {
        $launcherProj = Join-Path $ScriptDir "MultiplayerLauncher.csproj"
        $launcherOut  = Join-Path $ScriptDir "dist"
        $code = Run-Proc "dotnet" "publish `"$launcherProj`" -c Release -o `"$launcherOut`" --nologo" $ScriptDir
        if ($code -eq 0) { Write-Log "Published to: $launcherOut" -Ok }
        else             { Write-Log "Build failed (exit $code)." -Err }
    }
})

$btnPublishLauncher.add_Click({
    $version = $txtVersion.Text.Trim()
    $notes   = $txtNotes.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        [Windows.Forms.MessageBox]::Show("Enter a version number.", "Error")
        return
    }
    Run-Operation "Build and Publish Launcher v$version" {

        function Invoke-DotnetPublish($Label, $ProjPath, $OutDir) {
            Write-Log "Building $Label..."
            $psi = New-Object Diagnostics.ProcessStartInfo
            $psi.FileName               = "dotnet"
            $psi.Arguments              = "publish `"$ProjPath`" -c Release -o `"$OutDir`" --nologo"
            $psi.UseShellExecute        = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError  = $true
            $psi.CreateNoWindow         = $true
            $psi.WorkingDirectory       = (Split-Path $ProjPath)
            $proc = [Diagnostics.Process]::Start($psi)
            while (-not $proc.StandardOutput.EndOfStream) {
                $line = $proc.StandardOutput.ReadLine()
                if ($line -and $line.Trim()) { Write-Log $line.Trim() }
                [Windows.Forms.Application]::DoEvents()
            }
            $errTxt = $proc.StandardError.ReadToEnd()
            $proc.WaitForExit()
            if ($errTxt) { foreach ($l in ($errTxt -split "`n")) { if ($l.Trim()) { Write-Log $l.Trim() -Err } } }
            return $proc.ExitCode
        }

        # ── Build MultiplayerLauncher ──────────────────────────────────────────
        $launcherProj = Join-Path $ScriptDir "MultiplayerLauncher.csproj"
        $launcherOut  = Join-Path $ScriptDir "dist"
        $code = Invoke-DotnetPublish "MultiplayerLauncher" $launcherProj $launcherOut
        if ($code -ne 0) { Write-Log "MultiplayerLauncher build failed." -Err; return }
        Write-Log "MultiplayerLauncher build succeeded." -Ok

        $launcherExe = Join-Path $launcherOut "MultiplayerLauncher.exe"
        if (-not (Test-Path $launcherExe)) { Write-Log "Exe not found: $launcherExe" -Err; return }

        # ── Build Bootstrap (Launcher.exe) ────────────────────────────────────
        $bootstrapProj = Join-Path $ScriptDir "Bootstrap\Bootstrap.csproj"
        $bootstrapOut  = Join-Path $ScriptDir "dist\Bootstrap"
        $code2 = Invoke-DotnetPublish "Bootstrap (Launcher.exe)" $bootstrapProj $bootstrapOut
        if ($code2 -ne 0) { Write-Log "Bootstrap build failed." -Err; return }
        Write-Log "Bootstrap build succeeded." -Ok

        $bootstrapExe = Join-Path $bootstrapOut "Launcher.exe"
        if (-not (Test-Path $bootstrapExe)) { Write-Log "Launcher.exe not found: $bootstrapExe" -Err; return }

        # ── GitHub Release ────────────────────────────────────────────────────
        Write-Log "Creating GitHub release v$version..."
        $tag   = "v$version"
        $code3 = Run-Proc "gh" "release create `"$tag`" `"$launcherExe`" `"$bootstrapExe`" --title `"$tag`" --notes `"$notes`"" $ScriptDir
        if ($code3 -eq 0) { Write-Log "Launcher $tag published to GitHub (MultiplayerLauncher.exe + Launcher.exe)." -Ok }
        else              { Write-Log "GitHub release failed (exit $code3). Is gh installed and authenticated?" -Err }
    }
})

$btnClearLog.add_Click({ $logBox.Clear() })

# ── Launch ─────────────────────────────────────────────────────────────────────
Update-ServerStatusLabel
Update-AllBrokerStatuses
Write-Log "Dev Publisher ready." -Ok
Write-Log "Select a game and build directory, then hit Publish."
Write-Log "Set broker paths in the Broker Management section to start/stop multiplayer servers."

[void]$form.ShowDialog()

} catch {
    [System.Windows.Forms.MessageBox]::Show(
        "DevTools failed to start:`n`n$($_.Exception.Message)`n`nLine: $($_.InvocationInfo.ScriptLineNumber)",
        "DevTools Error",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error
    )
}
