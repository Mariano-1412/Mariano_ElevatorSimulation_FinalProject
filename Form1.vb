Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Collections.Generic
Imports System.Windows.Forms

Public Class Form1


    Private Const FLOOR_COUNT As Integer = 4
    Private Const FLOOR_HEIGHT As Integer = 105
    Private Const CAR_WIDTH As Integer = 92
    Private Const CAR_HEIGHT As Integer = 78
    Private Const SHAFT_TOP As Integer = 38
    Private Const SHAFT_LEFT As Integer = 300
    Private Const SHAFT_WIDTH As Integer = 125
    Private Const MOVE_SPEED As Integer = 4
    Private Const DOOR_SPEED As Integer = 5

    Private Const MAX_PEOPLE As Integer = 10
    Private Const MAX_WEIGHT As Integer = 800
    Private Const VERSION_TEXT As String = "v2"

    Private Class Rider
        Public Id As Integer
        Public Origin As Integer
        Public Target As Integer
        Public Direction As Integer
        Public Weight As Integer

        Public Sub New(idValue As Integer, originValue As Integer, targetValue As Integer, weightValue As Integer)
            Id = idValue
            Origin = originValue
            Target = targetValue
            Weight = weightValue
            If Target > Origin Then
                Direction = 1
            Else
                Direction = -1
            End If
        End Sub

        Public Function ShortText() As String
            Return "F" & Target.ToString() & "(" & Weight.ToString() & "kg)"
        End Function
    End Class

    Private buildingPanel As Panel
    Private shaftPanel As Panel
    Private carPanel As Panel
    Private doorLeft As Panel
    Private doorRight As Panel
    Private controlPanel As Panel

    Private lblCurrentFloor As Label
    Private lblDirection As Label
    Private lblDoorStatus As Label
    Private lblMessage As Label
    Private lblUp As Label
    Private lblDown As Label
    Private lblPeople As Label
    Private lblWeight As Label
    Private lblQueue As Label
    Private lblOnboard As Label
    Private lblOverload As Label
    Private lblVersion As Label
    Private lblCarLoad As Label

    Private btnF1 As Button
    Private btnF2 As Button
    Private btnF3 As Button
    Private btnF4 As Button
    Private btnOpen As Button
    Private btnClose As Button
    Private btnAlarm As Button
    Private btnEmergency As Button
    Private btnReset As Button
    Private btnAddRider As Button
    Private btnRemoveRider As Button

    Private moveTimer As New Timer()
    Private doorTimer As New Timer()
    Private pauseTimer As New Timer()
    Private overloadFlashTimer As New Timer()

    Private currentFloor As Integer = 1
    Private displayFloor As Integer = 1
    Private targetFloor As Integer = 1

    Private isMoving As Boolean = False
    Private movementDirection As Integer = 0
    Private serviceDirection As Integer = 0
    Private emergencyStop As Boolean = False
    Private doorMode As String = "Closed"
    Private doorGap As Integer = 0
    Private exchangeDoneThisStop As Boolean = False
    Private overloadFlashOn As Boolean = False

    Private nextRiderId As Integer = 1
    Private ReadOnly onboard As New List(Of Rider)()
    Private waiting(FLOOR_COUNT) As List(Of Rider)
    Private ReadOnly rng As New Random()

    Private manualCarCalls(FLOOR_COUNT) As Boolean
    Private carCalls(FLOOR_COUNT) As Boolean
    Private hallUpCalls(FLOOR_COUNT) As Boolean
    Private hallDownCalls(FLOOR_COUNT) As Boolean

    Private ReadOnly insideButtons As New Dictionary(Of Integer, Button)()
    Private ReadOnly hallButtons As New Dictionary(Of String, Button)()
    Private waitingLabels(FLOOR_COUNT) As Label

    Public Sub New()
        InitializeComponent()
        Me.Controls.Clear()
        InitializeWaitingLists()
        BuildInterface()
        SetupTimers()
        ResetElevatorView()
    End Sub

    Private Sub InitializeWaitingLists()
        For floor As Integer = 1 To FLOOR_COUNT
            waiting(floor) = New List(Of Rider)()
        Next
    End Sub

    Private Sub BuildInterface()
        Me.Text = "Smart Elevator Simulation System"
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.ClientSize = New Size(920, 670)
        Me.BackColor = Color.White
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False

        buildingPanel = New Panel()
        buildingPanel.Location = New Point(25, 25)
        buildingPanel.Size = New Size(520, 530)
        buildingPanel.BackColor = Color.White
        buildingPanel.BorderStyle = BorderStyle.FixedSingle
        Me.Controls.Add(buildingPanel)

        DrawBuildingFloors()
        CreateHallCallButtons()
        CreateShaftAndCar()
        CreateControlPanel()
    End Sub

    Private Sub DrawBuildingFloors()
        For i As Integer = 0 To FLOOR_COUNT
            Dim y As Integer = SHAFT_TOP + (i * FLOOR_HEIGHT)
            Dim line As New Panel()
            line.Location = New Point(20, y)
            line.Size = New Size(465, 2)
            line.BackColor = Color.FromArgb(80, 80, 80)
            buildingPanel.Controls.Add(line)
        Next

        For floor As Integer = 1 To FLOOR_COUNT
            Dim floorY As Integer = SHAFT_TOP + ((FLOOR_COUNT - floor) * FLOOR_HEIGHT) + 23

            Dim lbl As New Label()
            lbl.Text = "FLOOR " & floor.ToString()
            lbl.Font = New Font("Segoe UI", 11, FontStyle.Bold)
            lbl.ForeColor = Color.Black
            lbl.BackColor = Color.White
            lbl.TextAlign = ContentAlignment.MiddleLeft
            lbl.Location = New Point(35, floorY)
            lbl.Size = New Size(105, 26)
            buildingPanel.Controls.Add(lbl)

            Dim waitLbl As New Label()
            waitLbl.Text = "Waiting: none"
            waitLbl.Font = New Font("Segoe UI", 8, FontStyle.Regular)
            waitLbl.ForeColor = Color.FromArgb(55, 55, 55)
            waitLbl.BackColor = Color.White
            waitLbl.TextAlign = ContentAlignment.MiddleLeft
            waitLbl.Location = New Point(35, floorY + 28)
            waitLbl.Size = New Size(210, 22)
            buildingPanel.Controls.Add(waitLbl)
            waitingLabels(floor) = waitLbl
        Next

        Dim hallTitle As New Label()
        hallTitle.Text = "HALL CALLS / WAITING RIDERS"
        hallTitle.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        hallTitle.ForeColor = Color.DimGray
        hallTitle.BackColor = Color.White
        hallTitle.TextAlign = ContentAlignment.MiddleCenter
        hallTitle.Location = New Point(135, 12)
        hallTitle.Size = New Size(180, 22)
        buildingPanel.Controls.Add(hallTitle)
    End Sub

    Private Sub CreateHallCallButtons()
        hallButtons.Clear()

        For floor As Integer = 1 To FLOOR_COUNT
            Dim floorY As Integer = SHAFT_TOP + ((FLOOR_COUNT - floor) * FLOOR_HEIGHT) + 25

            If floor < FLOOR_COUNT Then
                Dim upButton As Button = MakeHallButton("▲", 165, floorY, floor, 1)
                buildingPanel.Controls.Add(upButton)
                hallButtons.Add(floor.ToString() & "_UP", upButton)
            End If

            If floor > 1 Then
                Dim downButton As Button = MakeHallButton("▼", 210, floorY, floor, -1)
                buildingPanel.Controls.Add(downButton)
                hallButtons.Add(floor.ToString() & "_DOWN", downButton)
            End If
        Next
    End Sub

    Private Function MakeHallButton(text As String, x As Integer, y As Integer, floor As Integer, direction As Integer) As Button
        Dim btn As New Button()
        btn.Text = text
        btn.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        btn.Location = New Point(x, y)
        btn.Size = New Size(36, 30)
        btn.BackColor = Color.WhiteSmoke
        btn.ForeColor = Color.Black
        btn.FlatStyle = FlatStyle.Standard
        btn.Tag = floor.ToString() & "," & direction.ToString()
        AddHandler btn.Click, AddressOf HallCallButton_Click
        Return btn
    End Function

    Private Sub CreateShaftAndCar()
        shaftPanel = New Panel()
        shaftPanel.Location = New Point(SHAFT_LEFT, SHAFT_TOP)
        shaftPanel.Size = New Size(SHAFT_WIDTH, FLOOR_HEIGHT * FLOOR_COUNT)
        shaftPanel.BackColor = Color.FromArgb(245, 245, 245)
        shaftPanel.BorderStyle = BorderStyle.FixedSingle
        buildingPanel.Controls.Add(shaftPanel)

        carPanel = New Panel()
        carPanel.Size = New Size(CAR_WIDTH, CAR_HEIGHT)
        carPanel.BackColor = Color.LightSteelBlue
        carPanel.BorderStyle = BorderStyle.FixedSingle
        shaftPanel.Controls.Add(carPanel)

        lblCarLoad = New Label()
        lblCarLoad.Text = "LIFT"
        lblCarLoad.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        lblCarLoad.TextAlign = ContentAlignment.MiddleCenter
        lblCarLoad.BackColor = Color.FromArgb(230, 240, 255)
        lblCarLoad.ForeColor = Color.Black
        lblCarLoad.Location = New Point(0, 0)
        lblCarLoad.Size = New Size(CAR_WIDTH, 21)
        carPanel.Controls.Add(lblCarLoad)

        doorLeft = New Panel()
        doorLeft.BackColor = Color.Gainsboro
        doorLeft.BorderStyle = BorderStyle.FixedSingle
        carPanel.Controls.Add(doorLeft)

        doorRight = New Panel()
        doorRight.BackColor = Color.Gainsboro
        doorRight.BorderStyle = BorderStyle.FixedSingle
        carPanel.Controls.Add(doorRight)
    End Sub

    Private Sub CreateControlPanel()
        controlPanel = New Panel()
        controlPanel.Location = New Point(565, 25)
        controlPanel.Size = New Size(330, 620)
        controlPanel.BackColor = Color.FromArgb(238, 238, 238)
        controlPanel.BorderStyle = BorderStyle.FixedSingle
        Me.Controls.Add(controlPanel)

        Dim title As New Label()
        title.Text = "SMART ELEVATOR"
        title.Font = New Font("Segoe UI", 15, FontStyle.Bold)
        title.TextAlign = ContentAlignment.MiddleCenter
        title.Location = New Point(10, 10)
        title.Size = New Size(305, 30)
        controlPanel.Controls.Add(title)

        lblVersion = New Label()
        lblVersion.Text = VERSION_TEXT
        lblVersion.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        lblVersion.TextAlign = ContentAlignment.MiddleCenter
        lblVersion.Location = New Point(20, 40)
        lblVersion.Size = New Size(290, 18)
        lblVersion.ForeColor = Color.Navy
        controlPanel.Controls.Add(lblVersion)

        lblCurrentFloor = MakeDisplayLabel("Current Floor: 1", 66)
        lblDirection = MakeDisplayLabel("Direction: Stop", 96)
        lblDoorStatus = MakeDisplayLabel("Door: Closed", 126)
        lblPeople = MakeDisplayLabel("People: 0 / " & MAX_PEOPLE.ToString(), 156)
        lblWeight = MakeDisplayLabel("Weight: 0 / " & MAX_WEIGHT.ToString() & " kg", 186)
        controlPanel.Controls.AddRange(New Control() {lblCurrentFloor, lblDirection, lblDoorStatus, lblPeople, lblWeight})

        lblOverload = New Label()
        lblOverload.Text = "LOAD OK"
        lblOverload.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        lblOverload.TextAlign = ContentAlignment.MiddleCenter
        lblOverload.Location = New Point(30, 218)
        lblOverload.Size = New Size(270, 26)
        lblOverload.BackColor = Color.FromArgb(205, 240, 205)
        lblOverload.ForeColor = Color.Black
        lblOverload.BorderStyle = BorderStyle.FixedSingle
        controlPanel.Controls.Add(lblOverload)

        lblUp = New Label()
        lblUp.Text = "▲ UP"
        lblUp.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        lblUp.TextAlign = ContentAlignment.MiddleCenter
        lblUp.Location = New Point(55, 252)
        lblUp.Size = New Size(92, 28)
        lblUp.BackColor = Color.LightGray
        lblUp.ForeColor = Color.DimGray
        lblUp.BorderStyle = BorderStyle.FixedSingle
        controlPanel.Controls.Add(lblUp)

        lblDown = New Label()
        lblDown.Text = "▼ DOWN"
        lblDown.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        lblDown.TextAlign = ContentAlignment.MiddleCenter
        lblDown.Location = New Point(183, 252)
        lblDown.Size = New Size(92, 28)
        lblDown.BackColor = Color.LightGray
        lblDown.ForeColor = Color.DimGray
        lblDown.BorderStyle = BorderStyle.FixedSingle
        controlPanel.Controls.Add(lblDown)

        btnF4 = MakeButton("4", 78, 292, AddressOf FloorButton_Click)
        btnF3 = MakeButton("3", 178, 292, AddressOf FloorButton_Click)
        btnF2 = MakeButton("2", 78, 340, AddressOf FloorButton_Click)
        btnF1 = MakeButton("1", 178, 340, AddressOf FloorButton_Click)
        controlPanel.Controls.AddRange(New Control() {btnF4, btnF3, btnF2, btnF1})
        insideButtons.Add(1, btnF1)
        insideButtons.Add(2, btnF2)
        insideButtons.Add(3, btnF3)
        insideButtons.Add(4, btnF4)

        btnOpen = MakeWideButton("OPEN", 45, 392, AddressOf OpenButton_Click)
        btnClose = MakeWideButton("CLOSE", 175, 392, AddressOf CloseButton_Click)
        controlPanel.Controls.AddRange(New Control() {btnOpen, btnClose})

        btnAddRider = MakeWideButton("ADD RIDER", 45, 431, AddressOf AddRiderButton_Click)
        btnRemoveRider = MakeWideButton("REMOVE", 175, 431, AddressOf RemoveRiderButton_Click)
        controlPanel.Controls.AddRange(New Control() {btnAddRider, btnRemoveRider})

        btnAlarm = MakeWideButton("ALARM", 45, 470, AddressOf AlarmButton_Click)
        btnEmergency = MakeWideButton("STOP", 175, 470, AddressOf EmergencyButton_Click)
        controlPanel.Controls.AddRange(New Control() {btnAlarm, btnEmergency})

        btnReset = MakeWideButton("RESET", 45, 507, AddressOf ResetButton_Click)
        btnReset.Width = 235
        btnReset.Height = 26
        controlPanel.Controls.Add(btnReset)

        lblQueue = New Label()
        lblQueue.Text = "Requests: none"
        lblQueue.Font = New Font("Segoe UI", 8, FontStyle.Regular)
        lblQueue.TextAlign = ContentAlignment.MiddleCenter
        lblQueue.Location = New Point(15, 539)
        lblQueue.Size = New Size(300, 30)
        lblQueue.ForeColor = Color.Black
        lblQueue.BackColor = Color.FromArgb(248, 248, 248)
        lblQueue.BorderStyle = BorderStyle.FixedSingle
        controlPanel.Controls.Add(lblQueue)

        lblOnboard = New Label()
        lblOnboard.Text = "Inside: none"
        lblOnboard.Font = New Font("Segoe UI", 8, FontStyle.Regular)
        lblOnboard.TextAlign = ContentAlignment.MiddleCenter
        lblOnboard.Location = New Point(15, 572)
        lblOnboard.Size = New Size(300, 24)
        lblOnboard.ForeColor = Color.Black
        lblOnboard.BackColor = Color.FromArgb(248, 248, 248)
        lblOnboard.BorderStyle = BorderStyle.FixedSingle
        controlPanel.Controls.Add(lblOnboard)

        lblMessage = New Label()
        lblMessage.Text = "Ready."
        lblMessage.Font = New Font("Segoe UI", 8, FontStyle.Regular)
        lblMessage.TextAlign = ContentAlignment.MiddleCenter
        lblMessage.Location = New Point(10, 598)
        lblMessage.Size = New Size(310, 18)
        lblMessage.ForeColor = Color.Black
        controlPanel.Controls.Add(lblMessage)
    End Sub

    Private Function MakeDisplayLabel(text As String, y As Integer) As Label
        Dim lbl As New Label()
        lbl.Text = text
        lbl.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        lbl.TextAlign = ContentAlignment.MiddleCenter
        lbl.Location = New Point(30, y)
        lbl.Size = New Size(270, 24)
        lbl.BackColor = Color.FromArgb(248, 252, 255)
        lbl.ForeColor = Color.Black
        lbl.BorderStyle = BorderStyle.FixedSingle
        Return lbl
    End Function

    Private Function MakeButton(text As String, x As Integer, y As Integer, handler As EventHandler) As Button
        Dim btn As New Button()
        btn.Text = text
        btn.Font = New Font("Segoe UI", 14, FontStyle.Bold)
        btn.Location = New Point(x, y)
        btn.Size = New Size(76, 40)
        btn.BackColor = Color.White
        btn.ForeColor = Color.Black
        btn.FlatStyle = FlatStyle.Standard
        AddHandler btn.Click, handler
        Return btn
    End Function

    Private Function MakeWideButton(text As String, x As Integer, y As Integer, handler As EventHandler) As Button
        Dim btn As New Button()
        btn.Text = text
        btn.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        btn.Location = New Point(x, y)
        btn.Size = New Size(105, 30)
        btn.BackColor = Color.White
        btn.ForeColor = Color.Black
        btn.FlatStyle = FlatStyle.Standard
        AddHandler btn.Click, handler
        Return btn
    End Function

    Private Sub SetupTimers()
        moveTimer.Interval = 20
        AddHandler moveTimer.Tick, AddressOf MoveTimer_Tick

        doorTimer.Interval = 20
        AddHandler doorTimer.Tick, AddressOf DoorTimer_Tick

        pauseTimer.Interval = 1600
        AddHandler pauseTimer.Tick, AddressOf PauseTimer_Tick

        overloadFlashTimer.Interval = 350
        AddHandler overloadFlashTimer.Tick, AddressOf OverloadFlashTimer_Tick
    End Sub

    Private Function FloorToCarTop(floor As Integer) As Integer
        Dim insideFloorTop As Integer = (FLOOR_COUNT - floor) * FLOOR_HEIGHT
        Return insideFloorTop + CInt((FLOOR_HEIGHT - CAR_HEIGHT) / 2)
    End Function

    Private Function CarTopToNearestFloor() As Integer
        Dim bestFloor As Integer = 1
        Dim bestDistance As Integer = Integer.MaxValue

        For floor As Integer = 1 To FLOOR_COUNT
            Dim d As Integer = Math.Abs(carPanel.Top - FloorToCarTop(floor))
            If d < bestDistance Then
                bestDistance = d
                bestFloor = floor
            End If
        Next

        Return bestFloor
    End Function

    Private Sub ResetElevatorView()
        currentFloor = 1
        displayFloor = 1
        targetFloor = 1
        isMoving = False
        movementDirection = 0
        serviceDirection = 0
        emergencyStop = False
        doorMode = "Closed"
        doorGap = 0
        exchangeDoneThisStop = False
        nextRiderId = 1
        onboard.Clear()
        For floor As Integer = 1 To FLOOR_COUNT
            waiting(floor).Clear()
        Next
        ClearAllRequests()
        overloadFlashTimer.Stop()
        overloadFlashOn = False

        carPanel.Left = CInt((shaftPanel.Width - CAR_WIDTH) / 2)
        carPanel.Top = FloorToCarTop(currentFloor)
        DrawDoors()
        UpdateDisplay("Ready.")
    End Sub

    Private Sub ClearAllRequests()
        For floor As Integer = 1 To FLOOR_COUNT
            manualCarCalls(floor) = False
            carCalls(floor) = False
            hallUpCalls(floor) = False
            hallDownCalls(floor) = False
        Next
    End Sub

    Private Sub DrawDoors()
        Dim halfWidth As Integer = CInt(CAR_WIDTH / 2)
        Dim leftWidth As Integer = Math.Max(0, halfWidth - doorGap)
        Dim rightWidth As Integer = Math.Max(0, halfWidth - doorGap)
        Dim doorTop As Integer = 23
        Dim doorHeight As Integer = CAR_HEIGHT - doorTop - 2

        doorLeft.Location = New Point(0, doorTop)
        doorLeft.Size = New Size(leftWidth, doorHeight)

        doorRight.Location = New Point(halfWidth + doorGap, doorTop)
        doorRight.Size = New Size(rightWidth, doorHeight)
    End Sub

    Private Function CurrentWeight() As Integer
        Dim total As Integer = 0
        For Each r As Rider In onboard
            total += r.Weight
        Next
        Return total
    End Function

    Private Function IsOverloaded() As Boolean
        Return onboard.Count > MAX_PEOPLE OrElse CurrentWeight() > MAX_WEIGHT
    End Function

    Private Sub UpdateDisplay(Optional message As String = "")
        displayFloor = CarTopToNearestFloor()

        If isMoving Then
            lblCurrentFloor.Text = "Current Floor: " & displayFloor.ToString()
        Else
            lblCurrentFloor.Text = "Current Floor: " & currentFloor.ToString()
        End If

        If emergencyStop Then
            lblDirection.Text = "Direction: Emergency Stop"
            lblDirection.ForeColor = Color.DarkRed
        ElseIf isMoving AndAlso movementDirection > 0 Then
            lblDirection.Text = "Direction: Up"
            lblDirection.ForeColor = Color.DarkGreen
        ElseIf isMoving AndAlso movementDirection < 0 Then
            lblDirection.Text = "Direction: Down"
            lblDirection.ForeColor = Color.DarkOrange
        ElseIf serviceDirection > 0 Then
            lblDirection.Text = "Direction: Up Sweep"
            lblDirection.ForeColor = Color.DarkGreen
        ElseIf serviceDirection < 0 Then
            lblDirection.Text = "Direction: Down Sweep"
            lblDirection.ForeColor = Color.DarkOrange
        Else
            lblDirection.Text = "Direction: Stop"
            lblDirection.ForeColor = Color.Black
        End If

        lblDoorStatus.Text = "Door: " & doorMode
        lblPeople.Text = "People: " & onboard.Count.ToString() & " / " & MAX_PEOPLE.ToString()
        lblWeight.Text = "Weight: " & CurrentWeight().ToString() & " / " & MAX_WEIGHT.ToString() & " kg"
        lblQueue.Text = RequestSummary()
        lblOnboard.Text = OnboardSummary()
        lblCarLoad.Text = "IN " & onboard.Count.ToString() & " | " & CurrentWeight().ToString() & "kg"

        UpdateWaitingLabels()
        UpdateLoadLabel()
        UpdateDirectionIndicators()

        If message <> "" Then
            lblMessage.Text = message
        End If

        HighlightRequestedButtons()
    End Sub

    Private Sub UpdateLoadLabel()
        If IsOverloaded() Then
            lblOverload.Text = "OVERLOAD ALARM"
            lblOverload.BackColor = Color.Red
            lblOverload.ForeColor = Color.White
            lblWeight.ForeColor = Color.DarkRed
            lblPeople.ForeColor = Color.DarkRed
        Else
            overloadFlashTimer.Stop()
            overloadFlashOn = False
            lblOverload.Text = "LOAD OK"
            lblOverload.BackColor = Color.FromArgb(205, 240, 205)
            lblOverload.ForeColor = Color.Black
            lblWeight.ForeColor = Color.Black
            lblPeople.ForeColor = Color.Black
        End If
    End Sub

    Private Sub UpdateDirectionIndicators()
        If isMoving AndAlso movementDirection > 0 Then
            lblUp.BackColor = Color.FromArgb(170, 230, 170)
            lblUp.ForeColor = Color.Black
            lblDown.BackColor = Color.LightGray
            lblDown.ForeColor = Color.DimGray
        ElseIf isMoving AndAlso movementDirection < 0 Then
            lblDown.BackColor = Color.FromArgb(255, 210, 140)
            lblDown.ForeColor = Color.Black
            lblUp.BackColor = Color.LightGray
            lblUp.ForeColor = Color.DimGray
        Else
            lblUp.BackColor = Color.LightGray
            lblUp.ForeColor = Color.DimGray
            lblDown.BackColor = Color.LightGray
            lblDown.ForeColor = Color.DimGray
        End If
    End Sub

    Private Sub UpdateWaitingLabels()
        For floor As Integer = 1 To FLOOR_COUNT
            Dim upCount As Integer = 0
            Dim downCount As Integer = 0
            For Each r As Rider In waiting(floor)
                If r.Direction > 0 Then
                    upCount += 1
                Else
                    downCount += 1
                End If
            Next
            If upCount = 0 AndAlso downCount = 0 Then
                waitingLabels(floor).Text = "Waiting: none"
            Else
                waitingLabels(floor).Text = "Waiting: ↑" & upCount.ToString() & "  ↓" & downCount.ToString()
            End If
        Next
    End Sub

    Private Function RequestSummary() As String
        Dim carList As New List(Of String)()
        Dim upList As New List(Of String)()
        Dim downList As New List(Of String)()

        For floor As Integer = 1 To FLOOR_COUNT
            If carCalls(floor) Then carList.Add(floor.ToString())
            If hallUpCalls(floor) Then upList.Add(floor.ToString())
            If hallDownCalls(floor) Then downList.Add(floor.ToString())
        Next

        Dim carText As String = If(carList.Count = 0, "-", String.Join(",", carList.ToArray()))
        Dim upText As String = If(upList.Count = 0, "-", String.Join(",", upList.ToArray()))
        Dim downText As String = If(downList.Count = 0, "-", String.Join(",", downList.ToArray()))
        Return "CAR[" & carText & "]  UP[" & upText & "]  DOWN[" & downText & "]"
    End Function

    Private Function OnboardSummary() As String
        If onboard.Count = 0 Then Return "Inside: none"
        Dim list As New List(Of String)()
        For Each r As Rider In onboard
            list.Add(r.ShortText())
        Next
        Return "Inside: " & String.Join("  ", list.ToArray())
    End Function

    Private Sub HighlightRequestedButtons()
        For floor As Integer = 1 To FLOOR_COUNT
            If insideButtons.ContainsKey(floor) Then
                If carCalls(floor) OrElse (isMoving AndAlso targetFloor = floor) Then
                    insideButtons(floor).BackColor = Color.LightYellow
                ElseIf floor = currentFloor AndAlso Not isMoving Then
                    insideButtons(floor).BackColor = Color.FromArgb(205, 240, 205)
                Else
                    insideButtons(floor).BackColor = Color.White
                End If
            End If

            Dim upKey As String = floor.ToString() & "_UP"
            If hallButtons.ContainsKey(upKey) Then
                hallButtons(upKey).BackColor = If(hallUpCalls(floor), Color.LightYellow, Color.WhiteSmoke)
            End If

            Dim downKey As String = floor.ToString() & "_DOWN"
            If hallButtons.ContainsKey(downKey) Then
                hallButtons(downKey).BackColor = If(hallDownCalls(floor), Color.LightYellow, Color.WhiteSmoke)
            End If
        Next
    End Sub

    Private Sub RebuildCarCalls()
        For floor As Integer = 1 To FLOOR_COUNT
            carCalls(floor) = manualCarCalls(floor)
        Next

        For Each r As Rider In onboard
            If r.Target >= 1 AndAlso r.Target <= FLOOR_COUNT Then
                carCalls(r.Target) = True
            End If
        Next
    End Sub

    Private Sub RebuildHallCalls()
        For floor As Integer = 1 To FLOOR_COUNT
            hallUpCalls(floor) = False
            hallDownCalls(floor) = False

            For Each r As Rider In waiting(floor)
                If r.Direction > 0 Then
                    hallUpCalls(floor) = True
                Else
                    hallDownCalls(floor) = True
                End If
            Next
        Next
    End Sub

    Private Function CreateRandomRider(origin As Integer, direction As Integer) As Rider
        Dim target As Integer
        If direction > 0 Then
            target = rng.Next(origin + 1, FLOOR_COUNT + 1)
        Else
            target = rng.Next(1, origin)
        End If

        Dim weight As Integer = rng.Next(55, 121)
        Dim r As New Rider(nextRiderId, origin, target, weight)
        nextRiderId += 1
        Return r
    End Function

    Private Function RandomDestinationFromCurrentFloor() As Integer
        Dim target As Integer = currentFloor
        While target = currentFloor
            target = rng.Next(1, FLOOR_COUNT + 1)
        End While
        Return target
    End Function

    Private Sub TriggerOverloadAlarm()
        System.Media.SystemSounds.Hand.Play()
        overloadFlashTimer.Start()
        pauseTimer.Stop()
        If Not isMoving AndAlso doorMode = "Closed" Then
            OpenDoors()
        End If
        UpdateDisplay("Overload. Remove rider before closing.")
    End Sub

    Private Sub AddRiderButton_Click(sender As Object, e As EventArgs)
        If emergencyStop Then
            UpdateDisplay("Press RESET first.")
            Return
        End If

        If isMoving Then
            UpdateDisplay("Cannot add rider while moving.")
            Return
        End If

        If doorMode <> "Open" Then
            UpdateDisplay("Open doors before rider enters.")
            Return
        End If

        Dim target As Integer = RandomDestinationFromCurrentFloor()
        Dim weight As Integer = rng.Next(55, 121)
        Dim r As New Rider(nextRiderId, currentFloor, target, weight)
        nextRiderId += 1
        onboard.Add(r)
        RebuildCarCalls()
        pauseTimer.Stop()

        If IsOverloaded() Then
            TriggerOverloadAlarm()
        Else
            UpdateDisplay("Rider entered: F" & currentFloor.ToString() & " to F" & target.ToString() & ".")
            pauseTimer.Start()
        End If
    End Sub

    Private Sub RemoveRiderButton_Click(sender As Object, e As EventArgs)
        If emergencyStop Then
            UpdateDisplay("Press RESET first.")
            Return
        End If

        If isMoving Then
            UpdateDisplay("Cannot remove rider while moving.")
            Return
        End If

        If doorMode <> "Open" Then
            UpdateDisplay("Open doors before rider exits.")
            Return
        End If

        If onboard.Count = 0 Then
            UpdateDisplay("No riders inside.")
            Return
        End If

        Dim removed As Rider = onboard(onboard.Count - 1)
        onboard.RemoveAt(onboard.Count - 1)
        RebuildCarCalls()
        UpdateDisplay("Removed rider going to F" & removed.Target.ToString() & ".")

        If Not IsOverloaded() Then
            pauseTimer.Start()
        End If
    End Sub

    Private Sub FloorButton_Click(sender As Object, e As EventArgs)
        If emergencyStop Then
            UpdateDisplay("Press RESET first.")
            Return
        End If

        Dim btn As Button = DirectCast(sender, Button)
        Dim selectedFloor As Integer = CInt(btn.Text)
        AddCarCall(selectedFloor)
    End Sub

    Private Sub HallCallButton_Click(sender As Object, e As EventArgs)
        If emergencyStop Then
            UpdateDisplay("Press RESET first.")
            Return
        End If

        Dim btn As Button = DirectCast(sender, Button)
        Dim parts() As String = CStr(btn.Tag).Split(","c)
        Dim selectedFloor As Integer = CInt(parts(0))
        Dim direction As Integer = CInt(parts(1))
        AddWaitingRider(selectedFloor, direction)
    End Sub

    Private Sub AddCarCall(floor As Integer)
        If floor = currentFloor AndAlso Not isMoving Then
            manualCarCalls(floor) = False
            RebuildCarCalls()
            OpenDoors()
            UpdateDisplay("Already on floor " & floor.ToString() & ".")
            Return
        End If

        manualCarCalls(floor) = True
        RebuildCarCalls()
        MaybeRetargetWhileMoving(floor)
        UpdateDisplay("Inside call: floor " & floor.ToString() & ".")
        DispatchElevator()
    End Sub

    Private Sub AddWaitingRider(floor As Integer, direction As Integer)
        If floor = FLOOR_COUNT AndAlso direction > 0 Then Return
        If floor = 1 AndAlso direction < 0 Then Return

        Dim r As Rider = CreateRandomRider(floor, direction)
        waiting(floor).Add(r)
        RebuildHallCalls()

        If floor = currentFloor AndAlso Not isMoving Then
            If serviceDirection = 0 Then serviceDirection = direction
            OpenDoors()
            UpdateDisplay("Rider waiting on F" & floor.ToString() & " to F" & r.Target.ToString() & ".")
            Return
        End If

        MaybeRetargetWhileMoving(floor)
        UpdateDisplay("Hall call: F" & floor.ToString() & " to F" & r.Target.ToString() & ".")
        DispatchElevator()
    End Sub

    Private Sub MaybeRetargetWhileMoving(newFloor As Integer)
        If Not isMoving OrElse movementDirection = 0 Then Return

        Dim nearestNow As Integer = CarTopToNearestFloor()

        If movementDirection > 0 Then
            If newFloor >= nearestNow AndAlso newFloor < targetFloor Then
                If carCalls(newFloor) OrElse hallUpCalls(newFloor) Then
                    targetFloor = newFloor
                    UpdateDisplay("Retargeted to nearer UP stop: F" & newFloor.ToString() & ".")
                End If
            End If
        ElseIf movementDirection < 0 Then
            If newFloor <= nearestNow AndAlso newFloor > targetFloor Then
                If carCalls(newFloor) OrElse hallDownCalls(newFloor) Then
                    targetFloor = newFloor
                    UpdateDisplay("Retargeted to nearer DOWN stop: F" & newFloor.ToString() & ".")
                End If
            End If
        End If
    End Sub

    Private Sub DispatchElevator()
        If emergencyStop OrElse isMoving Then Return

        If doorMode = "Open" OrElse doorMode = "Opening" Then
            If IsOverloaded() Then
                TriggerOverloadAlarm()
            Else
                pauseTimer.Stop()
                CloseDoors()
            End If
            Return
        End If

        If doorMode <> "Closed" Then Return

        If IsOverloaded() Then
            TriggerOverloadAlarm()
            Return
        End If

        targetFloor = ChooseNextStop()

        If targetFloor = 0 Then
            serviceDirection = 0
            movementDirection = 0
            UpdateDisplay("Ready.")
            Return
        End If

        If targetFloor = currentFloor Then
            OpenDoors()
            Return
        End If

        movementDirection = If(targetFloor > currentFloor, 1, -1)
        If serviceDirection = 0 Then serviceDirection = movementDirection
        isMoving = True
        moveTimer.Start()
        UpdateDisplay("Smart sweep to floor " & targetFloor.ToString() & ".")
    End Sub

    Private Function ChooseNextStop() As Integer
        RebuildCarCalls()
        RebuildHallCalls()

        If Not HasAnyRequest() Then Return 0

        If serviceDirection = 0 Then
            serviceDirection = ChooseStartingDirection()
        End If

        If serviceDirection > 0 Then
            Dim upStop As Integer = FindNextUpStop()
            If upStop <> 0 Then Return upStop

            If HasAnyRequestBelow(currentFloor) OrElse HasWaitingDirection(currentFloor, -1) Then
                serviceDirection = -1
                Return FindNextDownStop()
            End If
        ElseIf serviceDirection < 0 Then
            Dim downStop As Integer = FindNextDownStop()
            If downStop <> 0 Then Return downStop

            If HasAnyRequestAbove(currentFloor) OrElse HasWaitingDirection(currentFloor, 1) Then
                serviceDirection = 1
                Return FindNextUpStop()
            End If
        End If

        Return 0
    End Function

    Private Function ChooseStartingDirection() As Integer
        If HasWaitingDirection(currentFloor, 1) Then Return 1
        If HasWaitingDirection(currentFloor, -1) Then Return -1

        Dim nearestFloor As Integer = 0
        Dim nearestDistance As Integer = Integer.MaxValue

        For floor As Integer = 1 To FLOOR_COUNT
            If HasRequestAtFloor(floor) Then
                Dim d As Integer = Math.Abs(floor - currentFloor)
                If d < nearestDistance Then
                    nearestDistance = d
                    nearestFloor = floor
                End If
            End If
        Next

        If nearestFloor = 0 Then Return 0
        If nearestFloor > currentFloor Then Return 1
        If nearestFloor < currentFloor Then Return -1
        Return 0
    End Function

    Private Function FindNextUpStop() As Integer
        If HasStopAtCurrentForDirection(1) Then Return currentFloor

        For floor As Integer = currentFloor + 1 To FLOOR_COUNT
            If carCalls(floor) OrElse hallUpCalls(floor) Then Return floor
        Next

        For floor As Integer = FLOOR_COUNT To currentFloor + 1 Step -1
            If hallDownCalls(floor) Then Return floor
        Next

        Return 0
    End Function

    Private Function FindNextDownStop() As Integer
        If HasStopAtCurrentForDirection(-1) Then Return currentFloor

        For floor As Integer = currentFloor - 1 To 1 Step -1
            If carCalls(floor) OrElse hallDownCalls(floor) Then Return floor
        Next

        For floor As Integer = 1 To currentFloor - 1
            If hallUpCalls(floor) Then Return floor
        Next

        Return 0
    End Function

    Private Function HasStopAtCurrentForDirection(direction As Integer) As Boolean
        If carCalls(currentFloor) Then Return True
        If direction > 0 AndAlso hallUpCalls(currentFloor) Then Return True
        If direction < 0 AndAlso hallDownCalls(currentFloor) Then Return True
        Return False
    End Function

    Private Function HasAnyRequest() As Boolean
        For floor As Integer = 1 To FLOOR_COUNT
            If HasRequestAtFloor(floor) Then Return True
        Next
        Return False
    End Function

    Private Function HasRequestAtFloor(floor As Integer) As Boolean
        Return carCalls(floor) OrElse hallUpCalls(floor) OrElse hallDownCalls(floor)
    End Function

    Private Function HasAnyRequestAbove(floorNumber As Integer) As Boolean
        For floor As Integer = floorNumber + 1 To FLOOR_COUNT
            If HasRequestAtFloor(floor) Then Return True
        Next
        Return False
    End Function

    Private Function HasAnyRequestBelow(floorNumber As Integer) As Boolean
        For floor As Integer = floorNumber - 1 To 1 Step -1
            If HasRequestAtFloor(floor) Then Return True
        Next
        Return False
    End Function

    Private Function HasWaitingDirection(floor As Integer, direction As Integer) As Boolean
        For Each r As Rider In waiting(floor)
            If r.Direction = direction Then Return True
        Next
        Return False
    End Function

    Private Sub NormalizeServiceDirectionAtStop()
        RebuildCarCalls()
        RebuildHallCalls()

        If serviceDirection > 0 Then
            If currentFloor = FLOOR_COUNT OrElse Not HasAnyRequestAbove(currentFloor) Then
                If hallDownCalls(currentFloor) OrElse HasAnyRequestBelow(currentFloor) Then
                    serviceDirection = -1
                End If
            End If
        ElseIf serviceDirection < 0 Then
            If currentFloor = 1 OrElse Not HasAnyRequestBelow(currentFloor) Then
                If hallUpCalls(currentFloor) OrElse HasAnyRequestAbove(currentFloor) Then
                    serviceDirection = 1
                End If
            End If
        Else
            serviceDirection = ChooseStartingDirection()
        End If
    End Sub

    Private Sub ProcessFloorExchange()
        If exchangeDoneThisStop Then Return
        exchangeDoneThisStop = True

        Dim exited As Integer = 0
        For i As Integer = onboard.Count - 1 To 0 Step -1
            If onboard(i).Target = currentFloor Then
                onboard.RemoveAt(i)
                exited += 1
            End If
        Next

        manualCarCalls(currentFloor) = False
        RebuildCarCalls()
        NormalizeServiceDirectionAtStop()

        Dim boardDirection As Integer = serviceDirection
        If boardDirection = 0 Then
            If HasWaitingDirection(currentFloor, 1) Then boardDirection = 1
            If boardDirection = 0 AndAlso HasWaitingDirection(currentFloor, -1) Then boardDirection = -1
        End If

        Dim boarded As Integer = 0
        If boardDirection <> 0 Then
            Dim list As List(Of Rider) = waiting(currentFloor)
            Dim index As Integer = 0

            While index < list.Count
                Dim r As Rider = list(index)
                If r.Direction = boardDirection Then
                    onboard.Add(r)
                    list.RemoveAt(index)
                    boarded += 1
                    RebuildCarCalls()

                    If IsOverloaded() Then
                        RebuildHallCalls()
                        TriggerOverloadAlarm()
                        UpdateDisplay("Boarded " & boarded.ToString() & ", overload triggered.")
                        Return
                    End If
                Else
                    index += 1
                End If
            End While
        End If

        RebuildCarCalls()
        RebuildHallCalls()

        If exited > 0 OrElse boarded > 0 Then
            UpdateDisplay("Exited: " & exited.ToString() & "  Boarded: " & boarded.ToString() & ".")
        Else
            UpdateDisplay("Doors open.")
        End If
    End Sub

    Private Sub MoveTimer_Tick(sender As Object, e As EventArgs)
        If emergencyStop Then
            moveTimer.Stop()
            isMoving = False
            UpdateDisplay("Emergency stop.")
            Return
        End If

        Dim targetTop As Integer = FloorToCarTop(targetFloor)

        If carPanel.Top < targetTop Then
            carPanel.Top = Math.Min(carPanel.Top + MOVE_SPEED, targetTop)
        ElseIf carPanel.Top > targetTop Then
            carPanel.Top = Math.Max(carPanel.Top - MOVE_SPEED, targetTop)
        End If

        If carPanel.Top = targetTop Then
            moveTimer.Stop()
            isMoving = False
            currentFloor = targetFloor
            movementDirection = 0
            exchangeDoneThisStop = False
            UpdateDisplay("Arrived at floor " & currentFloor.ToString() & ".")
            OpenDoors()
        Else
            UpdateDisplay()
        End If
    End Sub

    Private Sub OpenButton_Click(sender As Object, e As EventArgs)
        If emergencyStop Then
            UpdateDisplay("Press RESET first.")
            Return
        End If

        If isMoving Then
            UpdateDisplay("Cannot open while moving.")
            Return
        End If

        OpenDoors()
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As EventArgs)
        If emergencyStop Then
            UpdateDisplay("Press RESET first.")
            Return
        End If

        If isMoving Then
            UpdateDisplay("Already moving.")
            Return
        End If

        CloseDoors()
    End Sub

    Private Sub OpenDoors()
        If isMoving OrElse emergencyStop Then Return

        pauseTimer.Stop()
        exchangeDoneThisStop = False
        doorMode = "Opening"
        doorTimer.Start()
        UpdateDisplay("Opening doors.")
    End Sub

    Private Sub CloseDoors()
        If isMoving OrElse emergencyStop Then Return

        If IsOverloaded() Then
            TriggerOverloadAlarm()
            Return
        End If

        pauseTimer.Stop()
        doorMode = "Closing"
        doorTimer.Start()
        UpdateDisplay("Closing doors.")
    End Sub

    Private Sub DoorTimer_Tick(sender As Object, e As EventArgs)
        Dim maxGap As Integer = CInt(CAR_WIDTH / 2) - 2

        If doorMode = "Opening" Then
            doorGap += DOOR_SPEED
            If doorGap >= maxGap Then
                doorGap = maxGap
                doorMode = "Open"
                doorTimer.Stop()
                ProcessFloorExchange()

                If IsOverloaded() Then
                    TriggerOverloadAlarm()
                Else
                    pauseTimer.Start()
                End If
            End If
        ElseIf doorMode = "Closing" Then
            If IsOverloaded() Then
                doorMode = "Opening"
                TriggerOverloadAlarm()
            Else
                doorGap -= DOOR_SPEED
                If doorGap <= 0 Then
                    doorGap = 0
                    doorMode = "Closed"
                    doorTimer.Stop()
                    UpdateDisplay("Doors closed.")
                    DispatchElevator()
                End If
            End If
        End If

        DrawDoors()
    End Sub

    Private Sub PauseTimer_Tick(sender As Object, e As EventArgs)
        pauseTimer.Stop()
        If Not emergencyStop AndAlso Not isMoving AndAlso doorMode = "Open" Then
            CloseDoors()
        End If
    End Sub

    Private Sub OverloadFlashTimer_Tick(sender As Object, e As EventArgs)
        If Not IsOverloaded() Then
            overloadFlashTimer.Stop()
            overloadFlashOn = False
            lblOverload.BackColor = Color.FromArgb(205, 240, 205)
            lblOverload.ForeColor = Color.Black
            Return
        End If

        overloadFlashOn = Not overloadFlashOn
        If overloadFlashOn Then
            lblOverload.BackColor = Color.Red
            lblOverload.ForeColor = Color.White
        Else
            lblOverload.BackColor = Color.Yellow
            lblOverload.ForeColor = Color.Black
        End If
    End Sub

    Private Sub AlarmButton_Click(sender As Object, e As EventArgs)
        System.Media.SystemSounds.Exclamation.Play()
        UpdateDisplay("Alarm button pressed.")
    End Sub

    Private Sub EmergencyButton_Click(sender As Object, e As EventArgs)
        emergencyStop = True
        isMoving = False
        movementDirection = 0
        serviceDirection = 0
        moveTimer.Stop()
        doorTimer.Stop()
        pauseTimer.Stop()
        overloadFlashTimer.Stop()
        doorMode = "Stopped"
        UpdateDisplay("Emergency stop active.")
    End Sub

    Private Sub ResetButton_Click(sender As Object, e As EventArgs)
        ResetElevatorView()
        UpdateDisplay("System reset.")
    End Sub

End Class
