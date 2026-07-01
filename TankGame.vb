Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

Public Class TankGame

    Public Const COLS As Integer = 13
    Public Const ROWS As Integer = 13
    Public Const ENEMY_TOTAL As Integer = 10      ' tong so dich phai tieu diet (PvAI)
    Public Const ENEMY_MAX_ALIVE As Integer = 3   ' so dich toi da cung luc tren san
    Public Const ENEMY_SPAWN_COOLDOWN As Integer = 45  ' so tick giua 2 lan spawn dich (45*120ms = ~5.4s)
    Public Const ENEMY_SHOOT_CHANCE As Integer = 18    ' % co hoi ban moi luot hanh dong
    Public Const ENEMY_SHOOT_COOLDOWN As Integer = 12  ' so tick toi thieu giua 2 lan ban cua 1 xe dich

    Public Enum CellType As Byte
        Empty = 0
        Brick = 1
        Steel = 2
        Water = 3
        Grass = 4   ' an tank, khong can tro di chuyen/dan
        Ice = 5     ' di chuyen binh thuong (khong lam truot trong phien ban nay)
        Base = 6    ' can cu (Dai bang) - trung dan la thua
    End Enum

    Public Enum Direction As Byte
        Up = 0
        Down = 1
        Left = 2
        Right = 3
    End Enum

    Public Structure BulletInfo
        Public X As Integer
        Public Y As Integer
        Public Dir As Direction
        Public Owner As Integer        ' 0/1 = player, -1 = dich AI
        Public EnemyIndex As Integer   ' chi dung khi Owner = -1
    End Structure

    Public Structure EnemyTank
        Public X As Integer
        Public Y As Integer
        Public Dir As Direction
        Public Alive As Boolean
        Public BulletActive As Boolean
        Public ActionCooldown As Integer
        Public ShootCooldown As Integer
    End Structure

    Public Structure BaseInfo
        Public X As Integer
        Public Y As Integer
        Public Alive As Boolean
    End Structure

    ' --- Ban do ---
    Public Map(COLS - 1, ROWS - 1) As CellType
    Private rng As New Random()

    ' --- Nguoi choi ---
    Public PlayerX(1) As Integer
    Public PlayerY(1) As Integer
    Public PlayerDir(1) As Direction
    Public PlayerAlive(1) As Boolean
    Public PlayerBulletActive(1) As Boolean

    ' --- Doi tuong ---
    Public Bullets As New List(Of BulletInfo)()
    Public Enemies As New List(Of EnemyTank)()
    Public Bases(1) As BaseInfo   ' Bases(0) = can cu Player1/PvAI, Bases(1) = can cu Player2 (chi dung trong PvP)

    ' --- Trang thai game ---
    Public GameOver As Boolean
    Public Winner As Integer       ' -1 = hoa/thua, 0 = P1 thang, 1 = P2 thang, 99 = het dich (PvAI)
    Public LastLog As String
    Public TickCount As Integer
    Public EnemiesSpawned As Integer
    Public EnemiesKilled As Integer

    ' --- Mode ---
    Public IsPvAI As Boolean

    Private enemySpawnTimer As Integer
    Private ReadOnly enemySpawnPoints As Point2D() = {
        New Point2D(1, 1), New Point2D(COLS \ 2, 1), New Point2D(COLS - 2, 1)
    }

    Public Structure Point2D
        Public X As Integer
        Public Y As Integer
        Public Sub New(px As Integer, py As Integer)
            X = px : Y = py
        End Sub
    End Structure

    Private ReadOnly DX() As Integer = {0, 0, -1, 1}   ' Up, Down, Left, Right
    Private ReadOnly DY() As Integer = {-1, 1, 0, 0}

    Public Sub New()
        ResetBoard()
    End Sub

    ' ============================================================
    '  KHOI TAO BAN DO
    ' ============================================================
    Public Sub ResetBoard()
        Dim x As Integer, y As Integer

        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                If x = 0 OrElse x = COLS - 1 OrElse y = 0 OrElse y = ROWS - 1 Then
                    Map(x, y) = CellType.Steel
                Else
                    Map(x, y) = CellType.Empty
                End If
            Next x
        Next y

        ' Rai gach + thep + nuoc + co ngau nhien o vung giua ban do
        Dim mapRng As New Random()
        For y = 2 To ROWS - 3
            For x = 2 To COLS - 3
                Dim safeP1 As Boolean = (x <= 2 AndAlso y >= ROWS - 3)
                Dim safeP2 As Boolean = (x >= COLS - 3 AndAlso y <= 2)
                Dim safeBase As Boolean = (Math.Abs(x - COLS \ 2) <= 1 AndAlso y >= ROWS - 3)
                If safeP1 OrElse safeP2 OrElse safeBase Then Continue For

                Dim roll As Integer = mapRng.Next(100)
                If roll < 35 Then
                    Map(x, y) = CellType.Brick
                ElseIf roll < 45 Then
                    Map(x, y) = CellType.Steel
                ElseIf roll < 52 Then
                    Map(x, y) = CellType.Water
                ElseIf roll < 60 Then
                    Map(x, y) = CellType.Grass
                ElseIf roll < 64 Then
                    Map(x, y) = CellType.Ice
                End If
            Next x
        Next y

        ' --- Can cu (Dai bang) Player 1 - giua, day ban do ---
        Dim b0x As Integer = COLS \ 2
        Dim b0y As Integer = ROWS - 2
        Map(b0x, b0y) = CellType.Base
        Bases(0).X = b0x : Bases(0).Y = b0y : Bases(0).Alive = True
        ' Hang gach bao quanh can cu (de bi pha dan, dung kieu ban goc)
        SurroundWithBrick(b0x, b0y)

        ' --- Can cu Player 2 (chi dung trong PvP) - giua, dinh ban do ---
        Dim b1x As Integer = COLS \ 2
        Dim b1y As Integer = 1
        Bases(1).X = b1x : Bases(1).Y = b1y : Bases(1).Alive = True

        ' --- Vi tri xuat phat ---
        PlayerX(0) = 1 : PlayerY(0) = ROWS - 2 : PlayerDir(0) = Direction.Up
        PlayerX(1) = COLS - 2 : PlayerY(1) = 1 : PlayerDir(1) = Direction.Down
        PlayerAlive(0) = True
        PlayerAlive(1) = True
        Map(PlayerX(0), PlayerY(0)) = CellType.Empty
        Map(PlayerX(1), PlayerY(1)) = CellType.Empty
        PlayerBulletActive(0) = False
        PlayerBulletActive(1) = False

        Bullets.Clear()
        Enemies.Clear()
        GameOver = False
        Winner = -1
        LastLog = "Bat dau! Mui ten/WASD di chuyen, Space ban."
        TickCount = 0
        EnemiesSpawned = 0
        EnemiesKilled = 0
        enemySpawnTimer = 5
    End Sub

    Private Sub SurroundWithBrick(bx As Integer, by As Integer)
        Dim ox As Integer, oy As Integer
        For oy = -1 To 1
            For ox = -1 To 1
                If ox = 0 AndAlso oy = 0 Then Continue For
                Dim nx As Integer = bx + ox
                Dim ny As Integer = by + oy
                If nx < 1 OrElse nx >= COLS - 1 OrElse ny < 1 OrElse ny >= ROWS - 1 Then Continue For
                Map(nx, ny) = CellType.Brick
            Next ox
        Next oy
    End Sub

    ' Goi sau ResetBoard khi choi PvAI (tuong tu SpawnMonsters cua BombGame)
    Public Sub StartPvAIWave()
        Enemies.Clear()
        EnemiesSpawned = 0
        EnemiesKilled = 0
        enemySpawnTimer = 5
        SpawnOneEnemy()
    End Sub

    Private Sub SpawnOneEnemy()
        If EnemiesSpawned >= ENEMY_TOTAL Then Return
        If CountAliveEnemies() >= ENEMY_MAX_ALIVE Then Return

        Dim sp As Point2D = enemySpawnPoints(rng.Next(enemySpawnPoints.Length))
        If Not IsCellFree(sp.X, sp.Y, -1) Then Return

        Dim e As New EnemyTank()
        e.X = sp.X : e.Y = sp.Y : e.Dir = Direction.Down
        e.Alive = True : e.BulletActive = False : e.ActionCooldown = 3 : e.ShootCooldown = 6
        Enemies.Add(e)
        EnemiesSpawned += 1
    End Sub

    ' ============================================================
    '  DI CHUYEN / BAN (goi tu input nguoi choi)
    ' ============================================================
    Public Function TryMove(player As Integer, dir As Direction) As Boolean
        If GameOver Then Return False
        If Not PlayerAlive(player) Then Return False

        PlayerDir(player) = dir   ' luon xoay huong, du co di chuyen duoc hay khong

        Dim nx As Integer = PlayerX(player) + DX(CInt(dir))
        Dim ny As Integer = PlayerY(player) + DY(CInt(dir))

        If Not IsCellWalkable(nx, ny) Then Return False
        If Not IsCellFree(nx, ny, player) Then Return False

        PlayerX(player) = nx
        PlayerY(player) = ny
        Return True
    End Function

    Public Function TryShoot(player As Integer) As Boolean
        If GameOver Then Return False
        If Not PlayerAlive(player) Then Return False
        If PlayerBulletActive(player) Then Return False

        Dim b As New BulletInfo()
        b.X = PlayerX(player) + DX(CInt(PlayerDir(player)))
        b.Y = PlayerY(player) + DY(CInt(PlayerDir(player)))
        b.Dir = PlayerDir(player)
        b.Owner = player
        b.EnemyIndex = -1

        If Not InBounds(b.X, b.Y) Then Return False
        If Map(b.X, b.Y) = CellType.Steel OrElse Map(b.X, b.Y) = CellType.Water Then Return False

        Bullets.Add(b)
        PlayerBulletActive(player) = True
        LastLog = String.Format("Player {0} khai hoa!", player + 1)
        Return True
    End Function

    Private Function IsCellWalkable(x As Integer, y As Integer) As Boolean
        If Not InBounds(x, y) Then Return False
        Select Case Map(x, y)
            Case CellType.Empty, CellType.Grass, CellType.Ice
                Return True
            Case Else
                Return False
        End Select
    End Function

    ' Kiem tra o co bi chiem boi tank khac (tru chinh minh, excludePlayer = -1 neu khong loai ai)
    Private Function IsCellFree(x As Integer, y As Integer, excludePlayer As Integer) As Boolean
        If excludePlayer <> 0 AndAlso PlayerAlive(0) AndAlso PlayerX(0) = x AndAlso PlayerY(0) = y Then Return False
        If excludePlayer <> 1 AndAlso PlayerAlive(1) AndAlso PlayerX(1) = x AndAlso PlayerY(1) = y Then Return False
        Dim i As Integer
        For i = 0 To Enemies.Count - 1
            If Enemies(i).Alive AndAlso Enemies(i).X = x AndAlso Enemies(i).Y = y Then Return False
        Next i
        Return True
    End Function

    Private Function InBounds(x As Integer, y As Integer) As Boolean
        Return x >= 0 AndAlso x < COLS AndAlso y >= 0 AndAlso y < ROWS
    End Function

    Private Function CountAliveEnemies() As Integer
        Dim cnt As Integer = 0
        Dim i As Integer
        For i = 0 To Enemies.Count - 1
            If Enemies(i).Alive Then cnt += 1
        Next i
        Return cnt
    End Function

    Public Function CountAliveEnemiesPublic() As Integer
        Return CountAliveEnemies()
    End Function

    ' ============================================================
    '  TICK CHINH - host goi dinh ky
    ' ============================================================
    Public Sub Tick()
        If GameOver Then Return
        TickCount += 1

        MoveBullets()
        ResolveBulletCollisions()

        If IsPvAI Then
            TickEnemyAI()
            TickEnemySpawn()
        End If

        CheckGameOver()
    End Sub

    Private Sub MoveBullets()
        Dim i As Integer = 0
        Do While i < Bullets.Count
            Dim b As BulletInfo = Bullets(i)
            Dim nx As Integer = b.X + DX(CInt(b.Dir))
            Dim ny As Integer = b.Y + DY(CInt(b.Dir))

            If Not InBounds(nx, ny) Then
                RemoveBullet(i)
                Continue Do
            End If

            Dim cell As CellType = Map(nx, ny)
            If cell = CellType.Steel Then
                RemoveBullet(i)
                Continue Do
            ElseIf cell = CellType.Brick Then
                Map(nx, ny) = CellType.Empty
                LastLog = "Pha vo mot mieng gach!"
                RemoveBullet(i)
                Continue Do
            ElseIf cell = CellType.Base Then
                Dim hitBase As Integer = If(nx = Bases(0).X AndAlso ny = Bases(0).Y, 0, 1)
                Bases(hitBase).Alive = False
                GameOver = True
                DetermineWinnerAfterBaseLoss()
                RemoveBullet(i)
                Continue Do
            End If

            b.X = nx : b.Y = ny
            Bullets(i) = b
            i += 1
        Loop
    End Sub

    Private Sub RemoveBullet(idx As Integer)
        Dim b As BulletInfo = Bullets(idx)
        If b.Owner >= 0 Then
            PlayerBulletActive(b.Owner) = False
        ElseIf b.EnemyIndex >= 0 AndAlso b.EnemyIndex < Enemies.Count Then
            Dim e As EnemyTank = Enemies(b.EnemyIndex)
            e.BulletActive = False
            Enemies(b.EnemyIndex) = e
        End If
        Bullets.RemoveAt(idx)
    End Sub

    Private Sub ResolveBulletCollisions()
        ' Dan vs Tank
        Dim i As Integer = 0
        Do While i < Bullets.Count
            Dim b As BulletInfo = Bullets(i)
            Dim hit As Boolean = False

            Dim p As Integer
            For p = 0 To 1
                If Not PlayerAlive(p) Then Continue For
                If b.Owner = p Then Continue For   ' khong tu ban trung minh
                If PlayerX(p) = b.X AndAlso PlayerY(p) = b.Y Then
                    PlayerAlive(p) = False
                    LastLog = String.Format("Player {0} bi ban ha!", p + 1)
                    hit = True
                End If
            Next p

            If Not hit Then
                Dim ei As Integer
                For ei = 0 To Enemies.Count - 1
                    If Not Enemies(ei).Alive Then Continue For
                    If b.EnemyIndex = ei Then Continue For
                    If Enemies(ei).X = b.X AndAlso Enemies(ei).Y = b.Y Then
                        Dim e As EnemyTank = Enemies(ei)
                        e.Alive = False
                        Enemies(ei) = e
                        EnemiesKilled += 1
                        LastLog = "Tieu diet 1 xe dich!"
                        hit = True
                    End If
                Next ei
            End If

            If hit Then
                RemoveBullet(i)
            Else
                i += 1
            End If
        Loop

        ' Dan vs Dan (khac chu thi huy nhau)
        i = 0
        Do While i < Bullets.Count
            Dim removed As Boolean = False
            Dim j As Integer = i + 1
            Do While j < Bullets.Count
                If Bullets(i).X = Bullets(j).X AndAlso Bullets(i).Y = Bullets(j).Y Then
                    RemoveBullet(j)
                    RemoveBullet(i)
                    removed = True
                    Exit Do
                End If
                j += 1
            Loop
            If Not removed Then i += 1
        Loop
    End Sub

    ' ============================================================
    '  AI DICH (PvAI)
    ' ============================================================
    Private Sub TickEnemyAI()
        Dim i As Integer
        For i = 0 To Enemies.Count - 1
            If Not Enemies(i).Alive Then Continue For
            Dim e As EnemyTank = Enemies(i)

            ' Cooldown ban giam moi tick, doc lap voi ActionCooldown
            If e.ShootCooldown > 0 Then e.ShootCooldown -= 1

            e.ActionCooldown -= 1
            If e.ActionCooldown > 0 Then
                Enemies(i) = e
                Continue For
            End If
            e.ActionCooldown = 3

            ' Chon huong: 50% huong ve phia player, 50% random
            Dim chosenDir As Direction = e.Dir
            If PlayerAlive(0) AndAlso rng.Next(100) < 50 Then
                chosenDir = DirectionToward(e.X, e.Y, PlayerX(0), PlayerY(0))
            Else
                chosenDir = CType(rng.Next(4), Direction)
            End If

            e.Dir = chosenDir
            Dim nx As Integer = e.X + DX(CInt(chosenDir))
            Dim ny As Integer = e.Y + DY(CInt(chosenDir))
            If IsCellWalkable(nx, ny) AndAlso IsEnemyCellFree(nx, ny, i) Then
                e.X = nx : e.Y = ny
            End If
            Enemies(i) = e

            ' Co hoi ban - chi ban khi khong con dan cu VA het cooldown rieng
            e = Enemies(i)
            If Not e.BulletActive AndAlso e.ShootCooldown <= 0 AndAlso rng.Next(100) < ENEMY_SHOOT_CHANCE Then
                EnemyShoot(i)
            End If
        Next i
    End Sub

    Private Function DirectionToward(sx As Integer, sy As Integer, tx As Integer, ty As Integer) As Direction
        Dim dx2 As Integer = tx - sx
        Dim dy2 As Integer = ty - sy
        If Math.Abs(dx2) > Math.Abs(dy2) Then
            Return If(dx2 > 0, Direction.Right, Direction.Left)
        Else
            Return If(dy2 > 0, Direction.Down, Direction.Up)
        End If
    End Function

    Private Function IsEnemyCellFree(x As Integer, y As Integer, excludeIdx As Integer) As Boolean
        If PlayerAlive(0) AndAlso PlayerX(0) = x AndAlso PlayerY(0) = y Then Return False
        If PlayerAlive(1) AndAlso PlayerX(1) = x AndAlso PlayerY(1) = y Then Return False
        Dim i As Integer
        For i = 0 To Enemies.Count - 1
            If i = excludeIdx Then Continue For
            If Enemies(i).Alive AndAlso Enemies(i).X = x AndAlso Enemies(i).Y = y Then Return False
        Next i
        Return True
    End Function

    Private Sub EnemyShoot(idx As Integer)
        Dim e As EnemyTank = Enemies(idx)
        Dim b As New BulletInfo()
        b.X = e.X + DX(CInt(e.Dir))
        b.Y = e.Y + DY(CInt(e.Dir))
        b.Dir = e.Dir
        b.Owner = -1
        b.EnemyIndex = idx

        If Not InBounds(b.X, b.Y) Then Return
        If Map(b.X, b.Y) = CellType.Steel OrElse Map(b.X, b.Y) = CellType.Water Then Return

        Bullets.Add(b)
        e.BulletActive = True
        e.ShootCooldown = ENEMY_SHOOT_COOLDOWN
        Enemies(idx) = e
    End Sub

    Private Sub TickEnemySpawn()
        If EnemiesSpawned >= ENEMY_TOTAL Then Return
        enemySpawnTimer -= 1
        If enemySpawnTimer <= 0 Then
            enemySpawnTimer = ENEMY_SPAWN_COOLDOWN
            SpawnOneEnemy()
        End If
    End Sub

    ' ============================================================
    '  KET THUC GAME
    ' ============================================================
    Private Sub CheckGameOver()
        If GameOver Then Return   ' da ket thuc tu su kien khac (vd: can cu bi pha) trong Tick nay

        If IsPvAI Then
            If Not PlayerAlive(0) Then
                GameOver = True : Winner = -1
                LastLog = "Xe tang cua ban bi pha huy! Thua cuoc!"
                Return
            End If
            If EnemiesSpawned >= ENEMY_TOTAL AndAlso CountAliveEnemies() = 0 Then
                GameOver = True : Winner = 99
                LastLog = String.Format("CHIEN THANG! Da tieu diet ca {0} xe dich!", ENEMY_TOTAL)
            End If
        Else
            Dim p0 As Boolean = PlayerAlive(0)
            Dim p1 As Boolean = PlayerAlive(1)
            If Not p0 AndAlso Not p1 Then
                GameOver = True : Winner = -1
                LastLog = "HOA! Ca 2 xe tang cung bi ha!"
            ElseIf Not p0 Then
                GameOver = True : Winner = 1
                LastLog = "Player 2 THANG! Xe Player 1 bi pha huy."
            ElseIf Not p1 Then
                GameOver = True : Winner = 0
                LastLog = "Player 1 THANG! Xe Player 2 bi pha huy."
            End If
        End If
    End Sub

    Private Sub DetermineWinnerAfterBaseLoss()
        If IsPvAI Then
            Winner = -1
            LastLog = "Can cu bi pha huy! Thua cuoc!"
        Else
            If Not Bases(0).Alive AndAlso Not Bases(1).Alive Then
                Winner = -1
                LastLog = "HOA! Ca 2 can cu cung bi pha!"
            ElseIf Not Bases(0).Alive Then
                Winner = 1
                LastLog = "Can cu Player 1 bi pha! Player 2 THANG!"
            Else
                Winner = 0
                LastLog = "Can cu Player 2 bi pha! Player 1 THANG!"
            End If
        End If
    End Sub

    ' ============================================================
    '  SERIALIZE / DESERIALIZE cho PvP mang
    ' ============================================================
    Public Function Serialize() As String
        Dim sb As New StringBuilder()

        Dim x As Integer, y As Integer
        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                sb.Append(CInt(Map(x, y)).ToString())
                If Not (x = COLS - 1 AndAlso y = ROWS - 1) Then sb.Append(",")
            Next x
        Next y
        sb.Append("|")

        sb.Append(PlayerX(0).ToString()) : sb.Append(",")
        sb.Append(PlayerY(0).ToString()) : sb.Append(",")
        sb.Append(CInt(PlayerDir(0)).ToString()) : sb.Append(",")
        sb.Append(If(PlayerAlive(0), "1", "0")) : sb.Append("|")

        sb.Append(PlayerX(1).ToString()) : sb.Append(",")
        sb.Append(PlayerY(1).ToString()) : sb.Append(",")
        sb.Append(CInt(PlayerDir(1)).ToString()) : sb.Append(",")
        sb.Append(If(PlayerAlive(1), "1", "0")) : sb.Append("|")

        Dim i As Integer
        For i = 0 To Bullets.Count - 1
            Dim b As BulletInfo = Bullets(i)
            sb.Append(b.X.ToString()) : sb.Append(",")
            sb.Append(b.Y.ToString()) : sb.Append(",")
            sb.Append(CInt(b.Dir).ToString()) : sb.Append(",")
            sb.Append(b.Owner.ToString())
            If i < Bullets.Count - 1 Then sb.Append(";")
        Next i
        sb.Append("|")

        For i = 0 To Enemies.Count - 1
            Dim e As EnemyTank = Enemies(i)
            sb.Append(e.X.ToString()) : sb.Append(",")
            sb.Append(e.Y.ToString()) : sb.Append(",")
            sb.Append(CInt(e.Dir).ToString()) : sb.Append(",")
            sb.Append(If(e.Alive, "1", "0"))
            If i < Enemies.Count - 1 Then sb.Append(";")
        Next i
        sb.Append("|")

        sb.Append(Bases(0).X.ToString()) : sb.Append(",")
        sb.Append(Bases(0).Y.ToString()) : sb.Append(",")
        sb.Append(If(Bases(0).Alive, "1", "0")) : sb.Append(";")
        sb.Append(Bases(1).X.ToString()) : sb.Append(",")
        sb.Append(Bases(1).Y.ToString()) : sb.Append(",")
        sb.Append(If(Bases(1).Alive, "1", "0")) : sb.Append("|")

        sb.Append(If(GameOver, "1", "0")) : sb.Append("|")
        sb.Append(Winner.ToString()) : sb.Append("|")
        sb.Append(EnemiesSpawned.ToString()) : sb.Append(",")
        sb.Append(EnemiesKilled.ToString()) : sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " "))

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        If parts.Length < 9 Then Return

        Dim mapParts As String() = parts(0).Split(","c)
        Dim idx As Integer = 0
        Dim x As Integer, y As Integer
        For y = 0 To ROWS - 1
            For x = 0 To COLS - 1
                If idx < mapParts.Length Then
                    Dim v As Integer = 0
                    Integer.TryParse(mapParts(idx), v)
                    Map(x, y) = CType(v, CellType)
                End If
                idx += 1
            Next x
        Next y

        Dim p0 As String() = parts(1).Split(","c)
        If p0.Length >= 4 Then
            Integer.TryParse(p0(0), PlayerX(0)) : Integer.TryParse(p0(1), PlayerY(0))
            Dim d0 As Integer = 0 : Integer.TryParse(p0(2), d0) : PlayerDir(0) = CType(d0, Direction)
            PlayerAlive(0) = (p0(3) = "1")
        End If
        Dim p1 As String() = parts(2).Split(","c)
        If p1.Length >= 4 Then
            Integer.TryParse(p1(0), PlayerX(1)) : Integer.TryParse(p1(1), PlayerY(1))
            Dim d1 As Integer = 0 : Integer.TryParse(p1(2), d1) : PlayerDir(1) = CType(d1, Direction)
            PlayerAlive(1) = (p1(3) = "1")
        End If

        Bullets.Clear()
        PlayerBulletActive(0) = False
        PlayerBulletActive(1) = False
        If parts(3).Length > 0 Then
            For Each entry As String In parts(3).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim bp As String() = entry.Split(","c)
                If bp.Length >= 4 Then
                    Dim b As New BulletInfo()
                    Integer.TryParse(bp(0), b.X) : Integer.TryParse(bp(1), b.Y)
                    Dim bd As Integer = 0 : Integer.TryParse(bp(2), bd) : b.Dir = CType(bd, Direction)
                    Integer.TryParse(bp(3), b.Owner)
                    b.EnemyIndex = -1
                    Bullets.Add(b)
                    If b.Owner = 0 Then PlayerBulletActive(0) = True
                    If b.Owner = 1 Then PlayerBulletActive(1) = True
                End If
            Next
        End If

        Enemies.Clear()
        If parts(4).Length > 0 Then
            For Each entry As String In parts(4).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim ep As String() = entry.Split(","c)
                If ep.Length >= 4 Then
                    Dim e As New EnemyTank()
                    Integer.TryParse(ep(0), e.X) : Integer.TryParse(ep(1), e.Y)
                    Dim ed As Integer = 0 : Integer.TryParse(ep(2), ed) : e.Dir = CType(ed, Direction)
                    e.Alive = (ep(3) = "1")
                    Enemies.Add(e)
                End If
            Next
        End If

        Dim baseParts As String() = parts(5).Split(";"c)
        If baseParts.Length >= 2 Then
            Dim bp0 As String() = baseParts(0).Split(","c)
            If bp0.Length >= 3 Then
                Integer.TryParse(bp0(0), Bases(0).X) : Integer.TryParse(bp0(1), Bases(0).Y)
                Bases(0).Alive = (bp0(2) = "1")
            End If
            Dim bp1 As String() = baseParts(1).Split(","c)
            If bp1.Length >= 3 Then
                Integer.TryParse(bp1(0), Bases(1).X) : Integer.TryParse(bp1(1), Bases(1).Y)
                Bases(1).Alive = (bp1(2) = "1")
            End If
        End If

        GameOver = (parts(6) = "1")
        Integer.TryParse(parts(7), Winner)

        If parts.Length >= 9 Then
            Dim cp As String() = parts(8).Split(","c)
            If cp.Length >= 2 Then
                Integer.TryParse(cp(0), EnemiesSpawned)
                Integer.TryParse(cp(1), EnemiesKilled)
            End If
        End If

        If parts.Length >= 10 Then LastLog = parts(9)
    End Sub

End Class
