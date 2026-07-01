Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class Form1
    Inherits Form

    Private Const DEFAULT_PORT As Integer = 9989
    Private Const CELL_SIZE As Integer = 36
    Private Const TICK_MS As Integer = 120

    Private game As TankGame
    Private peer As NetworkPeer
    Private isHost As Boolean
    Private localPlayer As Integer = -1
    Private isPvAIMode As Boolean = False

    Private BoardW As Integer = TankGame.COLS * CELL_SIZE
    Private BoardH As Integer = TankGame.ROWS * CELL_SIZE

    ' === UI mode select ===
    Private pnlMode As Panel

    ' === UI connect (PvP) ===
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblStatus As Label

    ' === UI game ===
    Private pnlGame As Panel
    Private boardPanel As DoubleBufferedPanel
    Private btnRestart As Button
    Private lstLog As ListBox

    ' === Chat (PvP only) ===
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private btnSend As Button
    Private Const CHAT_W As Integer = 210

    ' === Match countdown ===
    Private Const MATCH_SECONDS As Integer = 180
    Private matchSecondsLeft As Integer = MATCH_SECONDS
    Private matchTimer As System.Windows.Forms.Timer
    Private lblCountdown As Label

    ' === Player card panels ===
    Private pnlCard0 As Panel
    Private pnlCard1 As Panel
    Private pnlSide As Panel

    ' === Timer ===
    Private tickTimer As System.Windows.Forms.Timer
    Private statePending As Boolean = False

    ' === Move cooldown (gioi han toc do xoay/di chuyen) ===
    Private moveTimer As System.Windows.Forms.Timer
    Private moveReady As Boolean = True
    Private Const MOVE_COOLDOWN_MS As Integer = 220

    ' === Pixel animation (truot tank vao o moi cho muot) ===
    Private Const RENDER_MS As Integer = 33
    Private Const SLIDE_SPEED As Single = 9.0!
    Private renderTimer As System.Windows.Forms.Timer
    Private waterAnimCounter As Integer = 0

    Private playerPX(1) As Single
    Private playerPY(1) As Single
    Private enemyPX() As Single
    Private enemyPY() As Single

    ' === Sprite (pixel-art PNG nap tu thu muc sprites/ canh file exe) ===
    Private spriteTankP0 As Image
    Private spriteTankP1 As Image
    Private spriteTankEnemy As Image
    Private spriteBrick As Image
    Private spriteSteel As Image
    Private spriteWater1 As Image
    Private spriteWater2 As Image
    Private spriteGrass As Image
    Private spriteIce As Image
    Private spriteBase As Image
    Private spriteBaseRuin As Image
    Private spriteBullet As Image
    Private spritesLoaded As Boolean = False
    Private waterFrameToggle As Boolean = False

    Private Sub LoadSprites()
        Try
            Dim dir As String = IO.Path.Combine(Application.StartupPath, "sprites")
            spriteTankP0 = Image.FromFile(IO.Path.Combine(dir, "tank_player0.png"))
            spriteTankP1 = Image.FromFile(IO.Path.Combine(dir, "tank_player1.png"))
            spriteTankEnemy = Image.FromFile(IO.Path.Combine(dir, "tank_enemy.png"))
            spriteBrick = Image.FromFile(IO.Path.Combine(dir, "brick.png"))
            spriteSteel = Image.FromFile(IO.Path.Combine(dir, "steel.png"))
            spriteWater1 = Image.FromFile(IO.Path.Combine(dir, "water1.png"))
            spriteWater2 = Image.FromFile(IO.Path.Combine(dir, "water2.png"))
            spriteGrass = Image.FromFile(IO.Path.Combine(dir, "grass.png"))
            spriteIce = Image.FromFile(IO.Path.Combine(dir, "ice.png"))
            spriteBase = Image.FromFile(IO.Path.Combine(dir, "base.png"))
            spriteBaseRuin = Image.FromFile(IO.Path.Combine(dir, "base_ruin.png"))
            spriteBullet = Image.FromFile(IO.Path.Combine(dir, "bullet.png"))
            spritesLoaded = True
        Catch ex As Exception
            ' Khong tim thay sprite -> fallback ve bang GDI+ hinh hoc nhu cu
            spritesLoaded = False
        End Try
    End Sub

    Public Sub New()
        InitUI()
    End Sub

    Private Sub InitUI()
        Me.Text = "Tank 1990 - 2CongLC"
        Me.ClientSize = New Size(BoardW + 20 + CHAT_W, BoardH + 160)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(20, 20, 20)
        Me.KeyPreview = True
        AddHandler Me.KeyDown, AddressOf Form1_KeyDown

        tickTimer = New System.Windows.Forms.Timer()
        tickTimer.Interval = TICK_MS
        AddHandler tickTimer.Tick, AddressOf TickTimer_Tick

        matchTimer = New System.Windows.Forms.Timer()
        matchTimer.Interval = 1000
        AddHandler matchTimer.Tick, AddressOf MatchTimer_Tick

        moveTimer = New System.Windows.Forms.Timer()
        moveTimer.Interval = MOVE_COOLDOWN_MS
        AddHandler moveTimer.Tick, Sub(s As Object, ev As EventArgs)
            moveReady = True
            moveTimer.Stop()
        End Sub

        renderTimer = New System.Windows.Forms.Timer()
        renderTimer.Interval = RENDER_MS
        AddHandler renderTimer.Tick, AddressOf RenderTimer_Tick

        BuildModePanel()
        BuildConnectPanel()
        BuildGamePanel()
        BuildSidePanel()
        BuildChatPanel()
        pnlConnect.Visible = False
        pnlGame.Visible = False
        LoadSprites()
    End Sub

    ' ============================================================
    '  MODE SELECT PANEL
    ' ============================================================
    Private Sub BuildModePanel()
        pnlMode = New Panel()
        pnlMode.Dock = DockStyle.Fill
        pnlMode.BackColor = Color.FromArgb(20, 20, 20)

        Dim lbl As New Label()
        lbl.Text = "TANK 1990"
        lbl.Font = New Font("Segoe UI", 24.0!, FontStyle.Bold)
        lbl.ForeColor = Color.Gold
        lbl.Location = New Point(220, 80) : lbl.AutoSize = True
        pnlMode.Controls.Add(lbl)

        Dim lbl2 As New Label()
        lbl2.Text = "Chon che do choi:"
        lbl2.Font = New Font("Segoe UI", 13.0!)
        lbl2.ForeColor = Color.LightGray
        lbl2.Location = New Point(265, 155) : lbl2.AutoSize = True
        pnlMode.Controls.Add(lbl2)

        Dim btnPvP As New Button()
        btnPvP.Text = "⚔  PvP - 2 Nguoi (LAN)"
        btnPvP.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        btnPvP.Location = New Point(215, 200) : btnPvP.Size = New Size(300, 60)
        btnPvP.BackColor = Color.SteelBlue : btnPvP.ForeColor = Color.White
        btnPvP.FlatStyle = FlatStyle.Flat
        AddHandler btnPvP.Click, AddressOf BtnPvP_Click
        pnlMode.Controls.Add(btnPvP)

        Dim btnPvAI As New Button()
        btnPvAI.Text = "🎖  PvAI - Bao Ve Can Cu"
        btnPvAI.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        btnPvAI.Location = New Point(215, 280) : btnPvAI.Size = New Size(300, 60)
        btnPvAI.BackColor = Color.FromArgb(160, 110, 20) : btnPvAI.ForeColor = Color.White
        btnPvAI.FlatStyle = FlatStyle.Flat
        AddHandler btnPvAI.Click, AddressOf BtnPvAI_Click
        pnlMode.Controls.Add(btnPvAI)

        Dim lHelp As New Label()
        lHelp.Text = "Dieu khien: WASD / Mui ten di chuyen  |  Space ban"
        lHelp.ForeColor = Color.Yellow
        lHelp.Font = New Font("Segoe UI", 9.0!)
        lHelp.Location = New Point(175, 375) : lHelp.AutoSize = True
        pnlMode.Controls.Add(lHelp)

        Me.Controls.Add(pnlMode)
    End Sub

    Private Sub BtnPvP_Click(sender As Object, e As EventArgs)
        isPvAIMode = False
        pnlMode.Visible = False
        pnlConnect.Visible = True
    End Sub

    Private Sub BtnPvAI_Click(sender As Object, e As EventArgs)
        isPvAIMode = True
        pnlMode.Visible = False
        StartPvAI()
    End Sub

    Private Sub StartPvAI()
        isHost = True
        localPlayer = 0
        game = New TankGame()
        game.IsPvAI = True
        game.StartPvAIWave()
        statePending = False
        moveReady = True
        moveTimer.Stop()
        InitPixelPos()
        ShowGamePanel()
        AppendLog(String.Format("PvAI: Bao ve can cu, tieu diet {0} xe dich de thang!", TankGame.ENEMY_TOTAL))
        tickTimer.Start()
        renderTimer.Start()
        ResetMatchTimer()
    End Sub

    ' ============================================================
    '  CONNECT PANEL (PvP)
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(20, 20, 20)

        Dim lbl As New Label()
        lbl.Text = "PvP - Ket Noi LAN"
        lbl.Font = New Font("Segoe UI", 20.0!, FontStyle.Bold)
        lbl.ForeColor = Color.SteelBlue
        lbl.Location = New Point(240, 70) : lbl.AutoSize = True
        pnlConnect.Controls.Add(lbl)

        Dim btnBack As New Button()
        btnBack.Text = "< Quay lai"
        btnBack.Location = New Point(10, 10) : btnBack.Size = New Size(100, 30)
        btnBack.BackColor = Color.DimGray : btnBack.ForeColor = Color.White
        btnBack.FlatStyle = FlatStyle.Flat
        AddHandler btnBack.Click, Sub(s As Object, ev As EventArgs)
            pnlConnect.Visible = False
            pnlMode.Visible = True
        End Sub
        pnlConnect.Controls.Add(btnBack)

        Dim lblPort As New Label()
        lblPort.Text = "Port:"
        lblPort.ForeColor = Color.LightGray
        lblPort.Location = New Point(230, 150) : lblPort.AutoSize = True
        pnlConnect.Controls.Add(lblPort)

        txtPort = New TextBox()
        txtPort.Text = DEFAULT_PORT.ToString()
        txtPort.Location = New Point(280, 147) : txtPort.Size = New Size(100, 25)
        pnlConnect.Controls.Add(txtPort)

        Dim lblIP As New Label()
        lblIP.Text = "IP doi thu (de Join):"
        lblIP.ForeColor = Color.LightGray
        lblIP.Location = New Point(195, 190) : lblIP.AutoSize = True
        pnlConnect.Controls.Add(lblIP)

        txtIP = New TextBox()
        txtIP.Location = New Point(195, 215) : txtIP.Size = New Size(185, 25)
        pnlConnect.Controls.Add(txtIP)

        btnHost = New Button()
        btnHost.Text = "Tao Phong (Host)"
        btnHost.Location = New Point(195, 260) : btnHost.Size = New Size(185, 40)
        btnHost.BackColor = Color.SeaGreen : btnHost.ForeColor = Color.White
        btnHost.FlatStyle = FlatStyle.Flat
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        btnJoin = New Button()
        btnJoin.Text = "Vao Phong (Join)"
        btnJoin.Location = New Point(195, 310) : btnJoin.Size = New Size(185, 40)
        btnJoin.BackColor = Color.SteelBlue : btnJoin.ForeColor = Color.White
        btnJoin.FlatStyle = FlatStyle.Flat
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblStatus = New Label()
        lblStatus.ForeColor = Color.Yellow
        lblStatus.Location = New Point(195, 365) : lblStatus.AutoSize = True
        pnlConnect.Controls.Add(lblStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    ' ============================================================
    '  GAME PANEL
    ' ============================================================
    Private Sub BuildGamePanel()
        pnlGame = New Panel()
        pnlGame.Location = New Point(10, 10)
        pnlGame.Size = New Size(BoardW, BoardH + 100)
        pnlGame.BackColor = Color.FromArgb(20, 20, 20)

        boardPanel = New DoubleBufferedPanel()
        boardPanel.Location = New Point(0, 0)
        boardPanel.Size = New Size(BoardW, BoardH)
        boardPanel.BackColor = Color.Black
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        pnlGame.Controls.Add(boardPanel)

        btnRestart = New Button()
        btnRestart.Text = "Choi Lai"
        btnRestart.Location = New Point(0, BoardH + 8) : btnRestart.Size = New Size(100, 30)
        btnRestart.BackColor = Color.DimGray : btnRestart.ForeColor = Color.White
        btnRestart.FlatStyle = FlatStyle.Flat
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        pnlGame.Controls.Add(btnRestart)

        lstLog = New ListBox()
        lstLog.Location = New Point(110, BoardH + 8) : lstLog.Size = New Size(BoardW - 110, 60)
        lstLog.BackColor = Color.FromArgb(35, 35, 35) : lstLog.ForeColor = Color.LightGray
        lstLog.BorderStyle = BorderStyle.FixedSingle
        pnlGame.Controls.Add(lstLog)

        Me.Controls.Add(pnlGame)
    End Sub

    Private Sub BuildSidePanel()
        pnlSide = New Panel()
        pnlSide.Location = New Point(BoardW + 20, 10)
        pnlSide.Size = New Size(CHAT_W, 130)
        pnlSide.BackColor = Color.FromArgb(20, 20, 20)
        pnlSide.Visible = False

        lblCountdown = New Label()
        lblCountdown.Font = New Font("Segoe UI", 22.0!, FontStyle.Bold)
        lblCountdown.ForeColor = Color.LimeGreen
        lblCountdown.Location = New Point(40, 0) : lblCountdown.AutoSize = True
        pnlSide.Controls.Add(lblCountdown)

        pnlCard0 = BuildPlayerCard("PLAYER 1", Color.DodgerBlue, New Point(0, 50))
        pnlSide.Controls.Add(pnlCard0)

        pnlCard1 = BuildPlayerCard("PLAYER 2", Color.OrangeRed, New Point(0, 95))
        pnlSide.Controls.Add(pnlCard1)

        Me.Controls.Add(pnlSide)
    End Sub

    Private Function BuildPlayerCard(title As String, accent As Color, loc As Point) As Panel
        Dim p As New Panel()
        p.Location = loc : p.Size = New Size(CHAT_W, 40)
        p.BackColor = Color.FromArgb(35, 35, 35)

        Dim bar As New Panel()
        bar.Location = New Point(0, 0) : bar.Size = New Size(4, 40)
        bar.BackColor = accent
        p.Controls.Add(bar)

        Dim lblTitle As New Label()
        lblTitle.Text = title
        lblTitle.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        lblTitle.ForeColor = accent
        lblTitle.Location = New Point(12, 4) : lblTitle.AutoSize = True
        p.Controls.Add(lblTitle)

        Dim lblStats As New Label()
        lblStats.Font = New Font("Segoe UI", 9.0!)
        lblStats.ForeColor = Color.LightGray
        lblStats.Location = New Point(12, 20) : lblStats.AutoSize = True
        p.Controls.Add(lblStats)

        Return p
    End Function

    Private Sub BuildChatPanel()
        pnlChat = New Panel()
        pnlChat.Location = New Point(BoardW + 20, 150)
        pnlChat.Size = New Size(CHAT_W, BoardH - 40)
        pnlChat.BackColor = Color.FromArgb(20, 20, 20)
        pnlChat.Visible = False

        lstChat = New ListBox()
        lstChat.Location = New Point(0, 0) : lstChat.Size = New Size(CHAT_W, BoardH - 80)
        lstChat.BackColor = Color.FromArgb(35, 35, 35) : lstChat.ForeColor = Color.LightGray
        lstChat.BorderStyle = BorderStyle.FixedSingle
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(0, BoardH - 75) : txtChatInput.Size = New Size(CHAT_W - 55, 25)
        AddHandler txtChatInput.KeyDown, Sub(s As Object, ev As KeyEventArgs)
            If ev.KeyCode = Keys.Enter Then
                BtnSend_Click(s, EventArgs.Empty)
                ev.Handled = True
                ev.SuppressKeyPress = True
            End If
        End Sub
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gui"
        btnSend.Location = New Point(CHAT_W - 50, BoardH - 76) : btnSend.Size = New Size(50, 27)
        btnSend.BackColor = Color.SteelBlue : btnSend.ForeColor = Color.White
        btnSend.FlatStyle = FlatStyle.Flat
        AddHandler btnSend.Click, AddressOf BtnSend_Click
        pnlChat.Controls.Add(btnSend)

        Me.Controls.Add(pnlChat)
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As EventArgs)
        If txtChatInput.Text.Trim() = "" Then Return
        Dim tag As String = If(localPlayer = 0, "Player 1", "Player 2")
        Dim msg As String = txtChatInput.Text.Trim()
        AppendChat(tag & ": " & msg)
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("CHAT:" & tag & ":" & msg)
        End If
        txtChatInput.Text = ""
    End Sub

    Private Sub AppendChat(msg As String)
        lstChat.Items.Add(msg)
        lstChat.TopIndex = lstChat.Items.Count - 1
    End Sub

    ' ============================================================
    '  VE BAN DO / DOI TUONG
    ' ============================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        If game Is Nothing Then Return
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.InterpolationMode = InterpolationMode.NearestNeighbor

        Dim x As Integer, y As Integer
        ' Lop 1: nen, gach, thep, nuoc, bang, can cu (KHONG ve co o lop nay - co phai ve sau tank)
        For y = 0 To TankGame.ROWS - 1
            For x = 0 To TankGame.COLS - 1
                If game.Map(x, y) <> TankGame.CellType.Grass Then DrawCell(g, x, y)
            Next x
        Next y

        ' Lop 2: tank (duoi co)
        If game.PlayerAlive(0) Then DrawTank(g, playerPX(0), playerPY(0), game.PlayerDir(0), 0)
        If game.PlayerAlive(1) Then DrawTank(g, playerPX(1), playerPY(1), game.PlayerDir(1), 1)

        Dim i As Integer
        For i = 0 To game.Enemies.Count - 1
            If game.Enemies(i).Alive Then
                Dim epx As Single = If(enemyPX IsNot Nothing AndAlso i < enemyPX.Length, enemyPX(i), CSng(game.Enemies(i).X * CELL_SIZE))
                Dim epy As Single = If(enemyPY IsNot Nothing AndAlso i < enemyPY.Length, enemyPY(i), CSng(game.Enemies(i).Y * CELL_SIZE))
                DrawTank(g, epx, epy, game.Enemies(i).Dir, -1)
            End If
        Next i

        ' Lop 3: dan
        For i = 0 To game.Bullets.Count - 1
            DrawBullet(g, game.Bullets(i).X, game.Bullets(i).Y)
        Next i

        ' Lop 4: co (an mot phan tank, dung kieu ban goc)
        For y = 0 To TankGame.ROWS - 1
            For x = 0 To TankGame.COLS - 1
                If game.Map(x, y) = TankGame.CellType.Grass Then DrawCell(g, x, y)
            Next x
        Next y
    End Sub

    Private Sub DrawCell(g As Graphics, x As Integer, y As Integer)
        Dim rx As Integer = x * CELL_SIZE
        Dim ry As Integer = y * CELL_SIZE
        Dim r As New Rectangle(rx, ry, CELL_SIZE, CELL_SIZE)

        If spritesLoaded Then
            Dim spr As Image = Nothing
            Select Case game.Map(x, y)
                Case TankGame.CellType.Brick : spr = spriteBrick
                Case TankGame.CellType.Steel : spr = spriteSteel
                Case TankGame.CellType.Water : spr = If(waterFrameToggle, spriteWater2, spriteWater1)
                Case TankGame.CellType.Ice : spr = spriteIce
                Case TankGame.CellType.Grass : spr = spriteGrass
                Case TankGame.CellType.Base
                    Dim isP0Base As Boolean = (x = game.Bases(0).X AndAlso y = game.Bases(0).Y)
                    Dim baseAlive As Boolean = If(isP0Base, game.Bases(0).Alive, game.Bases(1).Alive)
                    spr = If(baseAlive, spriteBase, spriteBaseRuin)
                Case Else
                    DrawFloorTile(g, x, y, rx, ry)
                    Return
            End Select
            g.DrawImage(spr, r)
            Return
        End If

        DrawCellLegacy(g, x, y)
    End Sub

    Private Sub DrawFloorTile(g As Graphics, x As Integer, y As Integer, rx As Integer, ry As Integer)
        Dim shade As Color = If((x + y) Mod 2 = 0, Color.FromArgb(28, 28, 28), Color.FromArgb(22, 22, 22))
        Using br As New SolidBrush(shade)
            g.FillRectangle(br, rx, ry, CELL_SIZE, CELL_SIZE)
        End Using
    End Sub

    ' Ve bang hinh hoc GDI+ (fallback khi khong tim thay file sprite/*.png)
    Private Sub DrawCellLegacy(g As Graphics, x As Integer, y As Integer)
        Dim rx As Integer = x * CELL_SIZE
        Dim ry As Integer = y * CELL_SIZE
        Dim r As New Rectangle(rx, ry, CELL_SIZE, CELL_SIZE)

        Select Case game.Map(x, y)
            Case TankGame.CellType.Brick
                Using br As New SolidBrush(Color.FromArgb(150, 90, 40))
                    g.FillRectangle(br, r)
                End Using
                Using p As New Pen(Color.FromArgb(90, 50, 20), 1)
                    g.DrawLine(p, rx, ry + CELL_SIZE \ 2, rx + CELL_SIZE, ry + CELL_SIZE \ 2)
                    g.DrawLine(p, rx + CELL_SIZE \ 2, ry, rx + CELL_SIZE \ 2, ry + CELL_SIZE \ 2)
                    g.DrawLine(p, rx + CELL_SIZE \ 4, ry + CELL_SIZE \ 2, rx + CELL_SIZE \ 4, ry + CELL_SIZE)
                    g.DrawLine(p, rx + 3 * CELL_SIZE \ 4, ry + CELL_SIZE \ 2, rx + 3 * CELL_SIZE \ 4, ry + CELL_SIZE)
                End Using
            Case TankGame.CellType.Steel
                Using br As New SolidBrush(Color.FromArgb(150, 150, 160))
                    g.FillRectangle(br, r)
                End Using
                Using p1 As New Pen(Color.FromArgb(220, 220, 230), 2)
                    g.DrawLine(p1, rx + 2, ry + 2, rx + CELL_SIZE \ 2, ry + 2)
                    g.DrawLine(p1, rx + 2, ry + 2, rx + 2, ry + CELL_SIZE \ 2)
                End Using
                Using p2 As New Pen(Color.FromArgb(70, 70, 80), 2)
                    g.DrawLine(p2, rx + CELL_SIZE - 2, ry + CELL_SIZE \ 2, rx + CELL_SIZE - 2, ry + CELL_SIZE - 2)
                    g.DrawLine(p2, rx + CELL_SIZE \ 2, ry + CELL_SIZE - 2, rx + CELL_SIZE - 2, ry + CELL_SIZE - 2)
                End Using
            Case TankGame.CellType.Water
                Using br As New SolidBrush(Color.FromArgb(40, 90, 200))
                    g.FillRectangle(br, r)
                End Using
                Using p As New Pen(Color.FromArgb(120, 170, 255), 1.5!)
                    g.DrawLine(p, rx + 4, ry + CELL_SIZE \ 3, rx + CELL_SIZE - 4, ry + CELL_SIZE \ 3)
                    g.DrawLine(p, rx + 4, ry + 2 * CELL_SIZE \ 3, rx + CELL_SIZE - 4, ry + 2 * CELL_SIZE \ 3)
                End Using
            Case TankGame.CellType.Ice
                Using br As New SolidBrush(Color.FromArgb(190, 225, 245))
                    g.FillRectangle(br, r)
                End Using
                Using p As New Pen(Color.FromArgb(150, 200, 230), 1)
                    g.DrawRectangle(p, rx + 2, ry + 2, CELL_SIZE - 5, CELL_SIZE - 5)
                End Using
            Case TankGame.CellType.Grass
                Using br As New SolidBrush(Color.FromArgb(150, 30, 120, 30))
                    g.FillRectangle(br, r)
                End Using
                Using p As New Pen(Color.FromArgb(150, 60, 160, 60), 1)
                    Dim k As Integer
                    For k = 0 To 3
                        Dim gx As Integer = rx + 4 + (k * (CELL_SIZE - 8) \ 3)
                        g.DrawLine(p, gx, ry + CELL_SIZE - 2, gx - 2, ry + 6)
                    Next k
                End Using
            Case TankGame.CellType.Base
                Using br As New SolidBrush(Color.FromArgb(40, 40, 40))
                    g.FillRectangle(br, r)
                End Using
                Using br2 As New SolidBrush(Color.Gold)
                    Dim pts() As PointF = {
                        New PointF(rx + CELL_SIZE \ 2, ry + 3),
                        New PointF(rx + CELL_SIZE - 4, ry + CELL_SIZE - 4),
                        New PointF(rx + 4, ry + CELL_SIZE - 4)
                    }
                    g.FillPolygon(br2, pts)
                End Using
                Using p As New Pen(Color.FromArgb(200, 160, 0), 1.5!)
                    g.DrawRectangle(p, rx + 1, ry + 1, CELL_SIZE - 3, CELL_SIZE - 3)
                End Using
            Case Else
                Dim shade As Color = If((x + y) Mod 2 = 0, Color.FromArgb(28, 28, 28), Color.FromArgb(22, 22, 22))
                Using br As New SolidBrush(shade)
                    g.FillRectangle(br, r)
                End Using
        End Select
    End Sub

    Private Sub DrawTank(g As Graphics, px As Single, py As Single, dir As TankGame.Direction, player As Integer)
        If spritesLoaded Then
            Dim spr As Image
            Select Case player
                Case 0 : spr = spriteTankP0
                Case 1 : spr = spriteTankP1
                Case Else : spr = spriteTankEnemy
            End Select

            Dim cx As Single = px + CELL_SIZE / 2.0!
            Dim cy As Single = py + CELL_SIZE / 2.0!
            g.TranslateTransform(cx, cy)
            Select Case dir
                Case TankGame.Direction.Up : g.RotateTransform(0)
                Case TankGame.Direction.Down : g.RotateTransform(180)
                Case TankGame.Direction.Left : g.RotateTransform(270)
                Case TankGame.Direction.Right : g.RotateTransform(90)
            End Select
            g.DrawImage(spr, -CELL_SIZE / 2.0!, -CELL_SIZE / 2.0!, CSng(CELL_SIZE), CSng(CELL_SIZE))
            g.ResetTransform()
            Return
        End If

        DrawTankLegacy(g, px, py, dir, player)
    End Sub

    ' Ve tank bang hinh hoc GDI+ (fallback khi khong tim thay sprite)
    Private Sub DrawTankLegacy(g As Graphics, px As Single, py As Single, dir As TankGame.Direction, player As Integer)
        Dim bodyClr As Color
        Select Case player
            Case 0 : bodyClr = Color.DodgerBlue
            Case 1 : bodyClr = Color.OrangeRed
            Case Else : bodyClr = Color.FromArgb(200, 60, 60)   ' dich AI
        End Select
        Dim darkClr As Color = Color.FromArgb(CInt(bodyClr.R * 0.5), CInt(bodyClr.G * 0.5), CInt(bodyClr.B * 0.5))

        Dim cx As Single = px + CELL_SIZE / 2.0!
        Dim cy As Single = py + CELL_SIZE / 2.0!
        Dim half As Single = CELL_SIZE / 2.0! - 3.0!

        g.TranslateTransform(cx, cy)
        Select Case dir
            Case TankGame.Direction.Up : g.RotateTransform(0)
            Case TankGame.Direction.Down : g.RotateTransform(180)
            Case TankGame.Direction.Left : g.RotateTransform(270)
            Case TankGame.Direction.Right : g.RotateTransform(90)
        End Select

        ' Xich xe (2 ben)
        Using br As New SolidBrush(darkClr)
            g.FillRectangle(br, -half, -half, 4.0!, half * 2.0!)
            g.FillRectangle(br, half - 4.0!, -half, 4.0!, half * 2.0!)
        End Using

        ' Than xe
        Using br As New SolidBrush(bodyClr)
            g.FillRectangle(br, -half + 4.0!, -half + 2.0!, half * 2.0! - 8.0!, half * 2.0! - 4.0!)
        End Using
        Using p As New Pen(darkClr, 1.2!)
            g.DrawRectangle(p, -half + 4.0!, -half + 2.0!, half * 2.0! - 8.0!, half * 2.0! - 4.0!)
        End Using

        ' Thap phao
        Using br As New SolidBrush(darkClr)
            g.FillEllipse(br, -5.0!, -5.0!, 10.0!, 10.0!)
        End Using

        ' Nong sung (huong len tren, da xoay theo Direction o tren)
        Using p As New Pen(Color.FromArgb(30, 30, 30), 3.0!)
            g.DrawLine(p, 0.0!, 0.0!, 0.0!, -half - 4.0!)
        End Using

        g.ResetTransform()
    End Sub

    Private Sub DrawBullet(g As Graphics, x As Integer, y As Integer)
        Dim cx As Integer = x * CELL_SIZE + CELL_SIZE \ 2
        Dim cy As Integer = y * CELL_SIZE + CELL_SIZE \ 2
        If spritesLoaded Then
            g.DrawImage(spriteBullet, cx - CELL_SIZE \ 2, cy - CELL_SIZE \ 2, CELL_SIZE, CELL_SIZE)
            Return
        End If
        DrawBulletLegacy(g, x, y)
    End Sub

    ' Ve dan bang hinh hoc GDI+ (fallback khi khong tim thay sprite)
    Private Sub DrawBulletLegacy(g As Graphics, x As Integer, y As Integer)
        Dim cx As Integer = x * CELL_SIZE + CELL_SIZE \ 2
        Dim cy As Integer = y * CELL_SIZE + CELL_SIZE \ 2
        Using br As New SolidBrush(Color.Yellow)
            g.FillEllipse(br, cx - 3, cy - 3, 6, 6)
        End Using
        Using p As New Pen(Color.FromArgb(180, 255, 220, 0), 1)
            g.DrawEllipse(p, cx - 4, cy - 4, 8, 8)
        End Using
    End Sub

    ' ============================================================
    '  INPUT
    ' ============================================================
    Private Sub Form1_KeyDown(sender As Object, e As KeyEventArgs)
        If game Is Nothing OrElse localPlayer < 0 Then Return
        If txtChatInput IsNot Nothing AndAlso txtChatInput.Focused Then Return

        Dim dir As TankGame.Direction = TankGame.Direction.Up
        Dim hasDir As Boolean = False
        Dim shoot As Boolean = False

        Select Case e.KeyCode
            Case Keys.W, Keys.Up : dir = TankGame.Direction.Up : hasDir = True
            Case Keys.S, Keys.Down : dir = TankGame.Direction.Down : hasDir = True
            Case Keys.A, Keys.Left : dir = TankGame.Direction.Left : hasDir = True
            Case Keys.D, Keys.Right : dir = TankGame.Direction.Right : hasDir = True
            Case Keys.Space : shoot = True
        End Select

        If shoot Then
            If isHost Then
                If game.TryShoot(localPlayer) Then
                    boardPanel.Invalidate()
                    statePending = True
                End If
            Else
                peer.SendLine("SHOOT:" & localPlayer.ToString())
            End If
            e.Handled = True
            Return
        End If

        If hasDir Then
            If Not moveReady Then Return
            moveReady = False
            moveTimer.Start()

            If isHost Then
                If game.TryMove(localPlayer, dir) Then
                    boardPanel.Invalidate()
                End If
                statePending = True
            Else
                peer.SendLine("MOVE:" & localPlayer.ToString() & ":" & CInt(dir).ToString())
            End If
            e.Handled = True
        End If
    End Sub

    ' ============================================================
    '  PIXEL ANIMATION
    ' ============================================================
    Private Sub InitPixelPos()
        If game Is Nothing Then Return
        playerPX(0) = CSng(game.PlayerX(0) * CELL_SIZE)
        playerPY(0) = CSng(game.PlayerY(0) * CELL_SIZE)
        playerPX(1) = CSng(game.PlayerX(1) * CELL_SIZE)
        playerPY(1) = CSng(game.PlayerY(1) * CELL_SIZE)

        ReDim enemyPX(Math.Max(game.Enemies.Count - 1, 0))
        ReDim enemyPY(Math.Max(game.Enemies.Count - 1, 0))
        Dim i As Integer
        For i = 0 To game.Enemies.Count - 1
            enemyPX(i) = CSng(game.Enemies(i).X * CELL_SIZE)
            enemyPY(i) = CSng(game.Enemies(i).Y * CELL_SIZE)
        Next i
    End Sub

    Private Function SlideToward(current As Single, target As Single, speed As Single) As Single
        Dim diff As Single = target - current
        If Math.Abs(diff) <= speed Then Return target
        Return current + Math.Sign(diff) * speed
    End Function

    Private Sub RenderTimer_Tick(sender As Object, e As EventArgs)
        If game Is Nothing Then Return

        waterAnimCounter += 1
        If waterAnimCounter >= 15 Then   ' ~500ms o 33ms/frame
            waterAnimCounter = 0
            waterFrameToggle = Not waterFrameToggle
        End If

        Dim ecount As Integer = game.Enemies.Count
        If enemyPX Is Nothing OrElse enemyPX.Length < ecount Then
            Dim oldPX() As Single = enemyPX
            Dim oldPY() As Single = enemyPY
            ReDim enemyPX(Math.Max(ecount - 1, 0))
            ReDim enemyPY(Math.Max(ecount - 1, 0))
            If oldPX IsNot Nothing Then
                Dim k As Integer
                For k = 0 To Math.Min(oldPX.Length, ecount) - 1
                    enemyPX(k) = oldPX(k)
                    enemyPY(k) = oldPY(k)
                Next k
            End If
        End If

        Dim changed As Boolean = False
        Dim i As Integer

        For i = 0 To 1
            If Not game.PlayerAlive(i) Then Continue For
            Dim tx As Single = CSng(game.PlayerX(i) * CELL_SIZE)
            Dim ty As Single = CSng(game.PlayerY(i) * CELL_SIZE)
            Dim nx As Single = SlideToward(playerPX(i), tx, SLIDE_SPEED)
            Dim ny As Single = SlideToward(playerPY(i), ty, SLIDE_SPEED)
            If nx <> playerPX(i) OrElse ny <> playerPY(i) Then
                playerPX(i) = nx : playerPY(i) = ny
                changed = True
            End If
        Next i

        For i = 0 To ecount - 1
            If Not game.Enemies(i).Alive Then Continue For
            Dim tx As Single = CSng(game.Enemies(i).X * CELL_SIZE)
            Dim ty As Single = CSng(game.Enemies(i).Y * CELL_SIZE)
            Dim nx As Single = SlideToward(enemyPX(i), tx, SLIDE_SPEED)
            Dim ny As Single = SlideToward(enemyPY(i), ty, SLIDE_SPEED)
            If nx <> enemyPX(i) OrElse ny <> enemyPY(i) Then
                enemyPX(i) = nx : enemyPY(i) = ny
                changed = True
            End If
        Next i

        ' Luon ve lai moi frame vi dan ban di chuyen lien tuc o tang Tick (khong chi o tang Render)
        boardPanel.Invalidate()
    End Sub

    ' ============================================================
    '  MATCH COUNTDOWN
    ' ============================================================
    Private Sub ResetMatchTimer()
        matchSecondsLeft = MATCH_SECONDS
        matchTimer.Stop()
        matchTimer.Start()
        UpdateCountdownLabel()
    End Sub

    Private Sub UpdateCountdownLabel()
        Dim mins As Integer = matchSecondsLeft \ 60
        Dim secs As Integer = matchSecondsLeft Mod 60
        lblCountdown.Text = String.Format("{0}:{1:D2}", mins, secs)
        If matchSecondsLeft <= 30 Then
            lblCountdown.ForeColor = Color.OrangeRed
        ElseIf matchSecondsLeft <= 60 Then
            lblCountdown.ForeColor = Color.Orange
        Else
            lblCountdown.ForeColor = Color.LimeGreen
        End If
    End Sub

    Private Sub MatchTimer_Tick(sender As Object, e As EventArgs)
        If game Is Nothing OrElse game.GameOver Then Return
        matchSecondsLeft -= 1
        UpdateCountdownLabel()
        If matchSecondsLeft <= 0 Then
            matchTimer.Stop()
            tickTimer.Stop()
            renderTimer.Stop()
            Dim result As String
            If isPvAIMode Then
                result = String.Format("Het gio! Da tieu diet {0}/{1} xe dich. Thua!", game.EnemiesKilled, TankGame.ENEMY_TOTAL)
            Else
                Dim alive0 As Boolean = game.PlayerAlive(0)
                Dim alive1 As Boolean = game.PlayerAlive(1)
                If alive0 AndAlso Not alive1 Then
                    result = "Het gio! Player 1 thang!"
                ElseIf alive1 AndAlso Not alive0 Then
                    result = "Het gio! Player 2 thang!"
                Else
                    result = "Het gio! Hoa! (Ca 2 xe con song)"
                End If
            End If
            AppendLog(result)
            Me.BeginInvoke(New Action(Sub()
                MessageBox.Show(result, "Het gio!")
            End Sub))
        End If
    End Sub

    ' ============================================================
    '  TICK TIMER
    ' ============================================================
    Private Sub TickTimer_Tick(sender As Object, e As EventArgs)
        If game Is Nothing OrElse Not isHost Then Return
        game.Tick()
        statePending = True
        boardPanel.Invalidate()
        RefreshInfo()

        If statePending AndAlso Not isPvAIMode Then
            BroadcastState()
            statePending = False
        End If

        If game.GameOver Then
            tickTimer.Stop()
            renderTimer.Stop()
            AppendLog(game.LastLog)
            If Not isPvAIMode Then BroadcastState()
            Me.BeginInvoke(New Action(Sub()
                MessageBox.Show(game.LastLog, "Ket thuc!")
            End Sub))
        End If
    End Sub

    ' ============================================================
    '  NETWORK (PvP only)
    ' ============================================================
    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        isHost = True : localPlayer = -1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        Try
            peer.StartHost(port)
            lblStatus.Text = "Dang cho doi thu tren port " & port.ToString() & "..."
        Catch ex As Exception
            MessageBox.Show("Loi: " & ex.Message)
        End Try
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then MessageBox.Show("Port khong hop le.") : Return
        If txtIP.Text.Trim() = "" Then MessageBox.Show("Nhap IP.") : Return
        isHost = False : localPlayer = 1
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected
        lblStatus.Text = "Dang ket noi..."
        peer.ConnectToHost(txtIP.Text.Trim(), port)
    End Sub

    Private Sub Peer_Connected()
        If Not isHost Then peer.SendLine("HELLO:Client")
    End Sub

    Private Sub Peer_Disconnected()
        tickTimer.Stop()
        Me.BeginInvoke(New Action(Sub()
            If game IsNot Nothing AndAlso game.GameOver Then Return
            MessageBox.Show("Mat ket noi.")
            pnlGame.Visible = False
            pnlSide.Visible = False
            pnlChat.Visible = False
            pnlConnect.Visible = False
            pnlMode.Visible = True
        End Sub))
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If Me.InvokeRequired Then
            Me.BeginInvoke(New Action(Of String)(AddressOf Peer_LineReceived), line)
            Return
        End If

        If line.StartsWith("HELLO") Then
            If isHost Then
                localPlayer = 0
                game = New TankGame()
                game.IsPvAI = False
                ShowGamePanel()
                statePending = False
                InitPixelPos()
                BroadcastState()
                tickTimer.Start()
                renderTimer.Start()
                ResetMatchTimer()
                AppendLog("Doi thu vao phong. Ban la Player 1 (xanh). WASD+Space de choi.")
            End If

        ElseIf line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New TankGame()
            game.Deserialize(line.Substring(6))
            If Not pnlGame.Visible Then
                ShowGamePanel()
                InitPixelPos()
                renderTimer.Start()
                ResetMatchTimer()
            End If
            boardPanel.Invalidate()
            RefreshInfo()
            If game.GameOver Then
                AppendLog(game.LastLog)
                MessageBox.Show(game.LastLog, "Ket thuc!")
            End If

        ElseIf line.StartsWith("MOVE:") Then
            If isHost Then
                Dim parts As String() = line.Substring(5).Split(":"c)
                If parts.Length >= 2 Then
                    Dim p, d As Integer
                    Integer.TryParse(parts(0), p)
                    Integer.TryParse(parts(1), d)
                    If game.TryMove(p, CType(d, TankGame.Direction)) Then
                        boardPanel.Invalidate()
                    End If
                    statePending = True
                    BroadcastState()
                End If
            End If

        ElseIf line.StartsWith("SHOOT:") Then
            If isHost Then
                Dim p As Integer
                Integer.TryParse(line.Substring(6), p)
                If game.TryShoot(p) Then
                    boardPanel.Invalidate()
                    BroadcastState()
                    AppendLog(game.LastLog)
                End If
            End If

        ElseIf line.StartsWith("CHAT:") Then
            Dim payload As String = line.Substring(5)
            Dim colon As Integer = payload.IndexOf(":"c)
            If colon >= 0 Then
                Dim tag As String = payload.Substring(0, colon)
                Dim msg As String = payload.Substring(colon + 1)
                AppendChat(tag & ": " & msg)
            End If

        End If
    End Sub

    Private Sub ShowGamePanel()
        pnlConnect.Visible = False
        pnlMode.Visible = False
        pnlGame.Visible = True
        pnlSide.Visible = True
        pnlChat.Visible = Not isPvAIMode
        pnlCard1.Visible = Not isPvAIMode
        lstChat.Items.Clear()
        RefreshInfo()
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        If Not isHost OrElse game Is Nothing Then Return
        tickTimer.Stop()
        matchTimer.Stop()
        game.ResetBoard()
        If isPvAIMode Then
            game.IsPvAI = True
            game.StartPvAIWave()
        End If
        statePending = False
        moveReady = True
        moveTimer.Stop()
        InitPixelPos()
        tickTimer.Start()
        renderTimer.Start()
        ResetMatchTimer()
        boardPanel.Invalidate()
        RefreshInfo()
        If Not isPvAIMode Then BroadcastState()
        AppendLog("Bat dau lai!")
    End Sub

    Private Sub RefreshInfo()
        If game Is Nothing Then Return
        Dim stats0 As Label = TryCast(pnlCard0.Controls(2), Label)
        If stats0 IsNot Nothing Then
            Dim alive0 As String = If(game.PlayerAlive(0), "Song", "Chet")
            Dim baseInfo As String = If(game.Bases(0).Alive, "Can cu: An toan", "Can cu: BI PHA")
            Dim killInfo As String = If(isPvAIMode, String.Format("  Diet: {0}/{1}", game.EnemiesKilled, TankGame.ENEMY_TOTAL), "")
            stats0.Text = String.Format("{0}  {1}{2}", alive0, baseInfo, killInfo)
            stats0.ForeColor = If(game.PlayerAlive(0), Color.LightGray, Color.Gray)
        End If
        If Not isPvAIMode Then
            Dim stats1 As Label = TryCast(pnlCard1.Controls(2), Label)
            If stats1 IsNot Nothing Then
                Dim alive1 As String = If(game.PlayerAlive(1), "Song", "Chet")
                Dim baseInfo1 As String = If(game.Bases(1).Alive, "Can cu: An toan", "Can cu: BI PHA")
                stats1.Text = String.Format("{0}  {1}", alive1, baseInfo1)
                stats1.ForeColor = If(game.PlayerAlive(1), Color.LightGray, Color.Gray)
            End If
        End If
    End Sub

    Private Sub AppendLog(msg As String)
        lstLog.Items.Add(msg)
        lstLog.TopIndex = lstLog.Items.Count - 1
    End Sub

End Class

' Panel co double buffering de chong nhay man hinh
Public Class DoubleBufferedPanel
    Inherits Panel
    Public Sub New()
        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer Or
                    ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.UserPaint, True)
        Me.UpdateStyles()
    End Sub
End Class
