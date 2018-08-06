Imports Microsoft.VisualBasic.FileIO
Imports CrystalWriter_RQ.CrystalWriterGlobal
Imports CrystalWriter_RQ.CrystalWriterGlobal.Globals
Imports CrystalWriter_RQ.Job

'*** CRYSTALWRITER RQ Overview ***
'
'CrystalWriter RQ provides a front-end, client application which interfaces with R-Quest's 
'"TrueNet FX API", the real power for burning CD's and DVD's. This application creates job request files
'(*.job) and places them into a SharedJobFolder, which is monitored by the Server Application.
'When TrueNet FX API discovers a new .job file, it begins burning the job. TrueNet FX API changes the name of
'the request file to reflect its current status:
'   ".job" = Job Request
'   ".hi"  = Hi-Priority Job Request
'   ".bsy" = Busy (in progress)
'   ".err" = Error performing job
'   ".bad" = Bad File (error parsing file)
'   ".don" = Done
'CrystalWriter RQ performs the following operations:
'1. Connects to the database.
'2. Detects whether the Server Application, TrueNet FX API Server, is running.
'3. Gets the next entry from the database queue.
'4. Overwrites the appropriate print file (.csv) to contain the custom print information.
'5. Creates the job request file.
'6. Repeats #3-#5 until there are already 2 jobs running.
'7. Waits for JOB files to be replaced by DON, BAD, or ERR files. Then it knows the job has completed.
'8. Deletes DON files (but leaves ERR files) from the SharedJobFolder.
'9. Repeats the process from #3 to #8 until the queue contains no more discs of the requested type(s).

Public Class frmCrystalWriter
    'LastQJob Subscript Constants
    Private Const SUB_QueueID As Integer = 0
    Private Const SUB_PlateNO As Integer = 1
    Private Const SUB_DiscType As Integer = 2
    Private Const SUB_TargetUserID As Integer = 3
    Private Const SUB_PackageName As Integer = 4

    'LastPrintInfo Subscript Constants
    Private Const SUB_UserID As Integer = 0
    Private Const SUB_PlateID As Integer = 1
    Private Const SUB_SampleInfo As Integer = 2
    Private Const SUB_SetupDate As Integer = 3
    Private Const SUB_FirstName As Integer = 4
    Private Const SUB_LastName As Integer = 5

    Private Enum ReadQueueVal
        ReadOK = 0
        ReadFailed = 1
        QueueEmpty = 2
    End Enum

    Public DriveStates As New Dictionary(Of Integer, String)

    Private LastQJob() As String                    'The latest Queue Job information.
    Private LastPrintInfo() As String               'The latest Print information.
    Private FinishedCDJobs As Integer = 0           'How many CD jobs were burned in this session.
    Private FinishedDVDJobs As Integer = 0          'How many DVD jobs were burned in this session.
    Private FailedCDJobs As Integer = 0             'How many CD jobs failed in this session.
    Private FailedDVDJobs As Integer = 0            'How many DVD jobs failed in this session.
    Private CurrentJobs As New List(Of Job)         'List of currently active jobs.
    Private FailedJobs As New List(Of Job)          'List of jobs that have failed.

    Private Sub mnuExit_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuExit.Click
        WriteLog("Exit menu was clicked.")
        CloseCrystalWriter()
    End Sub 'Exits the program.

    Private Sub chkJPEG_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkJPEG.CheckedChanged
        WriteLog("JPEG jobs were " & IIf(chkJPEG.Checked, "en", "dis") & "abled.")
        For Each C As Control In grpJPEG.Controls
            If C.Name <> "chkJPEG" Then C.Enabled = chkJPEG.Checked
        Next
        AtLeastOneDiscTypeSelected()
    End Sub 'Select JPEG images.

    Private Sub chkTIF_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkTIF.CheckedChanged
        WriteLog("TIF jobs were " & IIf(chkTIF.Checked, "en", "dis") & "abled.")
        For Each C As Control In grpTIF.Controls
            If C.Name <> "chkTIF" Then C.Enabled = chkTIF.Checked
        Next
        AtLeastOneDiscTypeSelected()
    End Sub 'Select TIF images.

    Private Sub AtLeastOneDiscTypeSelected()
        If Not chkJPEG.Checked AndAlso Not chkTIF.Checked Then btnSendJob.Enabled = False Else btnSendJob.Enabled = True
    End Sub 'Enables btnSendJob if at least one disc type is checked.

    Private Sub btnBrowseJPEGSource_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnBrowseJPEGSource.Click
        With BrowseFolder
            .SelectedPath = txtJPEGSource.Text
            .ShowDialog()
            txtJPEGSource.Text = .SelectedPath
            If Not txtJPEGSource.Text.EndsWith("\") Then txtJPEGSource.Text &= "\"
        End With
    End Sub 'Allows user to browse to a custom JPEG directory.

    Private Sub btnBrowseTIFSource_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnBrowseTIFSource.Click
        With BrowseFolder
            .SelectedPath = txtTIFSource.Text
            .ShowDialog()
            txtTIFSource.Text = .SelectedPath
            If Not txtTIFSource.Text.EndsWith("\") Then txtTIFSource.Text &= "\"
        End With
    End Sub 'Allows user to browse to a custom TIF directory.

    Private Sub frmCrystalWriter_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        GetSettings()
        'Delete the old log file, if one exists. 
        If FileSystem.FileExists(Application.StartupPath & "\CW.log") Then FileSystem.DeleteFile(Application.StartupPath & "\CW.log")
        Try 'Delete any DON files that might exist.
            Kill(SharedJobFolder & "\*.don")
        Catch ex As Exception
        End Try

        'Add all dictionary definitions.
        DriveStates.Add(0, "Idle")
        DriveStates.Add(1, "Recording")
        DriveStates.Add(2, "Reading")
        DriveStates.Add(3, "Verifying")
        DriveStates.Add(4, "Disc Loaded")
        DriveStates.Add(5, "Verify Failed")
        DriveStates.Add(6, "Verify Complete")
        DriveStates.Add(7, "Record Failed")
        DriveStates.Add(8, "Record Complete")       
        While Not Connect2DB()
            MsgBox("Could not connect to the database.", MsgBoxStyle.OkOnly, "Unable to connect")
            If MsgBox("Would you like to change the connection?", MsgBoxStyle.YesNo, "Database Connection") = MsgBoxResult.No Then
                End
            End If
            dlgChangeDB.ShowDialog()
            If dlgChangeDB.DialogResult <> Windows.Forms.DialogResult.OK Then End
        End While
        CheckServerApp.ShowDialog() 'This makes sure that the server application is running.
        DiscTimer.Enabled = True
        WriteLog("CrystalWriter RQ was started.")
        'SendPTMessage("CHECK_DISCSINBIN")
        Try
            crystalwriter_PT.WordPrint.StartWord()
            WriteLog("MS Word was opened for printing.", txtLog)
        Catch ex As Exception
            WriteLog("Could not open MS Word for printing: " & ex.Message, txtLog)
            Me.Dispose()
        End Try
    End Sub 'Program execution begins HERE.

    Private Sub frmCrystalWriter_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Disposed
        WriteLog("Main window was closed.")
        CloseCrystalWriter()
    End Sub 'Program execution ends HERE.

    Private Sub GetSettings()
        SharedJobFolder = "I:\TNFXAPI_JOBS"
        txtJPEGSource.Text = "I:\JpgImages\"
        txtTIFSource.Text = "I:\TifImages\"
    End Sub 'Sets some important values to their defaults.

    Private Function Connect2DB() As Boolean
        Dim DBaseString As String = "", UIDString As String = "", PWDString As String = ""
        Dim ReportTrayString As String = "", LetterTrayString As String = ""
        Dim Coder As New Encryption

        Try
            Dim fileContents() As String = Split(My.Computer.FileSystem.ReadAllText(Application.StartupPath & "\CWSettings.ini"), vbCrLf)
            For lin As Integer = 0 To fileContents.Length - 1
                GrabINIValue(fileContents(lin), "DBase", DBaseString)
                GrabINIValue(fileContents(lin), "UID", UIDString)
                GrabINIValue(fileContents(lin), "PWord", PWDString)
            Next
            DBaseString = Coder.Decrypt(DBaseString)
            UIDString = Coder.Decrypt(UIDString)
            PWDString = Coder.Decrypt(PWDString)

            'Configure and open our database connection
            conn = New Data.Odbc.OdbcConnection
            'Connect to MySQL Server using  MySQL ODBC 5.1 Driver
            conn.ConnectionString = "DRIVER={MySQL ODBC 5.1 Driver};" _
                                    & "SERVER=mysql.dhcp.hwi.buffalo.edu;" _
                                    & "DATABASE=" & DBaseString & ";" _
                                    & "UID=" & UIDString & ";PWD=" & PWDString & "; OPTION=43"
            conn.Open()
            WriteLog("Database connection was opened successfully.", txtLog)
            Return True
        Catch Ex As Exception
            WriteLog("There was an error when connecting to the database: " & Ex.Message, txtLog)
            Return False
        End Try
    End Function 'Defines and opens a connection to the database.

    Private Function ReadQueue(ByVal DiscType As String, ByVal Row As Integer) As ReadQueueVal
        If ExecuteQuery("select * from discqueue where disctype='" & DiscType & "' order by plate_name limit 3") Then
            Dim QData As DataSet = TempDataSet
            If QData.Tables(0).Rows.Count = 0 Then Return ReadQueueVal.QueueEmpty
            If QData.Tables(0).Rows.Count >= Row + 1 Then
                ReDim LastQJob(4)
                For c As Integer = 0 To LastQJob.Length - 1
                    LastQJob(c) = QData.Tables(0).Rows(Row).ItemArray(c).ToString
                Next c
                WriteLog("Job " & Join(LastQJob) & " was read from the queue.")
                Return ReadQueueVal.ReadOK
            End If
            WriteLog("There are no more jobs in the queue.")
            Return ReadQueueVal.QueueEmpty
        Else
            WriteLog("A new job could not be read from the queue.")
            Return ReadQueueVal.ReadFailed
        End If
    End Function 'Puts the next DB queue entry into LastQJob.

    Private Function GetPrintInfo() As Boolean
        If ExecuteQuery("select user.uid, plate_info.plateid, plate_info.contactsamplenum, " & _
            "plate_info.setupdate, user.firstname, user.lastname " & _
            "from plate_info, user where plate_name='" & LastQJob(SUB_PlateNO) & _
            "' and plate_info.uid=user.uid") Then
            Dim PrintData As DataSet = TempDataSet
            If PrintData.Tables(0).Rows.Count > 0 Then
                ReDim LastPrintInfo(5)
                For c As Integer = 0 To LastPrintInfo.Length - 1
                    LastPrintInfo(c) = PrintData.Tables(0).Rows(0).ItemArray(c).ToString
                Next c
                LastPrintInfo(SUB_SetupDate) = Split(LastPrintInfo(SUB_SetupDate))(0)
                WriteLog("Print info " & Join(LastPrintInfo) & " was obtained from the database.")
                Return True
            End If
            WriteLog("Print information could not be obtained from the database.", txtLog)
            Return False
        Else
            WriteLog("Print information could not be obtained from the database.", txtLog)
            Return False
        End If
    End Function 'Puts the print information into LastPrintInfo

    Private Function GetNumInQueue(ByVal DiscType As DiscTypeEnum) As Integer
        Dim DiscTypeStr As String = IIf(DiscType = DiscTypeEnum.DVD, "DVD", "CD")
        If ExecuteQuery("select * from discqueue where disctype='" & DiscTypeStr & "' order by plate_name") Then
            Return TempDataSet.Tables(0).Rows.Count
        Else
            Return -1
        End If
    End Function 'Gets the # of jobs in the DB queue for the specified disc type.

    Private Sub BurnDisc(ByVal DiscType As DiscTypeEnum, ByVal CurrentJobNo As Integer, ByVal PlateNum As String, ByVal FirstName As String, ByVal LastName As String, ByVal SampleInfo As String, ByVal SetupDate As String)
        Dim ImageType As String = IIf(DiscType = DiscTypeEnum.DVD, "TIF", "JPEG")
        Dim DiscTypeStr As String = IIf(DiscType = DiscTypeEnum.DVD, "DVD", "CD")
        Dim FileText As String
        If DiscType = DiscTypeEnum.CD Then
            FileText = _
            "DEVICE=" & "NS2100" & vbCrLf & _
            "JOB_TYPE=" & "BUILD+COPY+PRINT" & vbCrLf & _
            "BUILD_TYPE=" & "ISO_CD" & vbCrLf & _
            "BUILD_PATH=" & txtJPEGSource.Text & PlateNum & vbCrLf & _
            "FIXATE=YES" & vbCrLf & _
            "FAST_START=NO" & vbCrLf & _
            "PRINT_TEMPLATE=" & SharedJobFolder & "\Labels\JPEG_label_1.rpt" & vbCrLf & _
            "PRINT_ENTRY_1=" & PlateNum & vbCrLf & _
            "PRINT_ENTRY_2=" & FirstName & " " & LastName & vbCrLf & _
            "PRINT_ENTRY_3=" & SampleInfo & vbCrLf & _
            "PRINT_ENTRY_4=" & SetupDate & vbCrLf & _
            "VOLUME=" & PlateNum
        Else
            FileText = _
            "DEVICE=" & "NS2100" & vbCrLf & _
            "JOB_TYPE=" & "BUILD+COPY+PRINT" & vbCrLf & _
            "BUILD_TYPE=" & "ISO_DVD_SL" & vbCrLf & _
            "BUILD_PATH=" & txtTIFSource.Text & PlateNum & vbCrLf & _
            "FIXATE=YES" & vbCrLf & _
            "FAST_START=NO" & vbCrLf & _
            "PRINT_TEMPLATE=" & SharedJobFolder & "\Labels\TIF_label_1.rpt" & vbCrLf & _
            "PRINT_ENTRY_1=" & PlateNum & vbCrLf & _
            "PRINT_ENTRY_2=" & FirstName & " " & LastName & vbCrLf & _
            "PRINT_ENTRY_3=" & SampleInfo & vbCrLf & _
            "PRINT_ENTRY_4=" & SetupDate & vbCrLf & _
            "VOLUME=" & PlateNum
        End If

        CurrentJobs.Add(New Job(CurrentJobNo, DiscType, PlateNum, FileText))
        WriteLog("Job CW_" & ImageType & "_" & PlateNum & " was created.", txtLog)
    End Sub 'Sends a JPEG/CD job to the SharedJobFolder.

    Private Sub DiscTimer_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles DiscTimer.Tick
        Dim Tj As New Job, RemoveIt As Boolean = False
        'Have 1 timer that checks every job (CD+DVD) for completion.
        For Each j As Job In CurrentJobs
            If FileSystem.FileExists(SharedJobFolder & j.JobID & ".DON") Or _
               FileSystem.FileExists(SharedJobFolder & j.JobID & ".ERR") Then
                'It's done!
                Tj = j
                RemoveIt = True
                WriteLog(Tj.JobID & " was completed as job #" & Tj.JobNO & ".")
            End If
        Next
        If RemoveIt Then
            If Not Tj.Destroy() Then
                FailedJobs.Add(Tj)
            End If
            CurrentJobs.Remove(Tj)
        End If
    End Sub 'Monitors the job files for completion

    Private Sub StatusTimer_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles StatusTimer.Tick
        If Not FileSystem.FileExists(SharedJobFolder & "\NS2100.rds") Then Exit Sub
        Dim StatusFile As String = TryReadStatusFile()
        Dim TempH As String = ""

        'System Status
        TempH = GetStatRec("SYSTEM_STATUS", StatusFile)
        If TempH <> "" Then lblSystemStatusStr.Text = TempH

        'Top Drive
        TempH = GetStatRec("RECORDER_1_PROGRESS", StatusFile, "0")
        If TempH <> "0" Then lblTopDriveStr.Text = GetStatRec("RECORDER_1_ACTIVITY", StatusFile) & " - " & TempH & "%"

        'Bottom Drive
        TempH = GetStatRec("RECORDER_0_PROGRESS", StatusFile, "0")
        If TempH <> "0" Then lblBottomDriveStr.Text = GetStatRec("RECORDER_1_ACTIVITY", StatusFile) & " - " & TempH & "%"

        'Free Hard Disk Space
        lblHDSpaceStr.Text = GetStatRec("HDD_SPACE", StatusFile, "0") & "MB"

        TempH = GetStatRec("PRINTER_CONSUMABLES", StatusFile)
        If (TempH <> "") Then
            Dim Inks As String() = TempH.Split(" ")
            'Black Ink Level
            lblBlack.Text = Inks(0).Split("=")(1)
            picBlackInk.Width = Int(CInt(lblBlack.Text.Replace("%", "")) / 2)

            'Color Ink Level
            TempH = GetStatRec("CartridgeFill1", StatusFile, "0")
            lblColor.Text = Inks(1).Split("=")(1)
            picCMYInk.Width = Int(CInt(lblColor.Text.Replace("%", "")) / 2)
        End If

        'CDs
        TempH = GetStatRec("HOPPER_1_COUNT", StatusFile, "0")
        lblCDBin.Text = "Left Bin - " & IIf(TempH = "-1", "?", TempH)
        lblCDDone.Text = FinishedCDJobs.ToString
        lblCDFailed.Text = FailedCDJobs.ToString

        'DVDs
        TempH = GetStatRec("HOPPER_2_COUNT", StatusFile, "0")
        lblDVDBin.Text = "Right Bin - " & IIf(TempH = "-1", "?", TempH)
        lblDVDDone.Text = FinishedDVDJobs.ToString
        lblDVDFailed.Text = FailedDVDJobs.ToString

        'Current Jobs
        For Each j As Job In CurrentJobs
            j.Update(StatusFile)
            If j.JobNO = 1 Then
                lblJob1ID.Text = j.XNumber
                lblJob1Disc.Text = j.DiscType
                lblJob1StartDate.Text = j.StartDate
                lblJob1StartTime.Text = Split(j.StartTime, ".")(0)
                If Not LCase(lblJob1Status.Text).Contains("rejecting") And LCase(j.Status).Contains("rejecting") Then
                    WriteLog("Job 1 rejected a bad " & j.DiscType & " disc.", txtLog)
                End If
                lblJob1Status.Text = j.Status
            ElseIf j.JobNO = 2 Then
                lblJob2ID.Text = j.XNumber
                lblJob2Disc.Text = j.DiscType
                lblJob2StartDate.Text = j.StartDate
                lblJob2StartTime.Text = Split(j.StartTime, ".")(0)
                If Not LCase(lblJob2Status.Text).Contains("rejecting") And LCase(j.Status).Contains("rejecting") Then
                    WriteLog("Job 2 rejected a bad " & j.DiscType & " disc.", txtLog)
                End If
                lblJob2Status.Text = j.Status
            End If
            If CurrentJobs.Count < 2 Then
                If j.JobNO = 1 Then
                    lblJob2ID.Text = "<NONE>"
                    lblJob2Disc.Text = ""
                    lblJob2StartDate.Text = ""
                    lblJob2StartTime.Text = ""
                    lblJob2Status.Text = ""
                ElseIf j.JobNO = 2 Then
                    lblJob1ID.Text = "<NONE>"
                    lblJob1Disc.Text = ""
                    lblJob1StartDate.Text = ""
                    lblJob1StartTime.Text = ""
                    lblJob1Status.Text = ""
                End If
            End If
        Next

        'Clear job information panels if there are no current jobs.
        If CurrentJobs.Count = 0 Then
            lblJob2ID.Text = "<NONE>"
            lblJob2Disc.Text = ""
            lblJob2StartDate.Text = ""
            lblJob2StartTime.Text = ""
            lblJob2Status.Text = ""
            lblJob1ID.Text = "<NONE>"
            lblJob1Disc.Text = ""
            lblJob1StartDate.Text = ""
            lblJob1StartTime.Text = ""
            lblJob1Status.Text = ""
        End If


        'Set the tool tips for controls that may be too small to see their entire contents.
        SetToolTips()

        'Enable/disable menus for aborting a current job.
        If CurrentJobs.Count >= 1 Then
            mnuJob1.Enabled = True
            mnuJob1.Text = "Job 1: (" & CurrentJobs(0).JobID & ")"
        Else
            mnuJob1.Enabled = False
            mnuJob1.Text = "(None)"
        End If
        If CurrentJobs.Count >= 2 Then
            mnuJob2.Enabled = True
            mnuJob2.Text = "Job 2: (" & CurrentJobs(1).JobID & ")"
        Else
            mnuJob2.Enabled = False
            mnuJob2.Text = "(None)"
        End If
    End Sub 'Monitors the status file created by the Server App.

    Private Function TryReadStatusFile() As String
RetryRead:
        Try
            TryReadStatusFile = My.Computer.FileSystem.ReadAllText(SharedJobFolder & "\NS2100.rds")
        Catch
            GoTo RetryRead
        End Try
    End Function 'Attempts (until successful) to read the status file.

    Private Function NextAvailableJobNo() As Integer
        Dim NumTaken As Boolean
        For n As Integer = 1 To 2
            NumTaken = False
            For Each j As Job In CurrentJobs
                If j.JobNO = n Then NumTaken = True
            Next
            If Not NumTaken Then Return n
        Next
    End Function 'Retrieves the next available job #; 

    Private Sub SetToolTips()
        For Each c As Control In grpJob1.Controls
            If c.Name.StartsWith("lbl") Then MyToolTip.SetToolTip(c, c.Text)
        Next
        For Each c As Control In grpJob2.Controls
            If c.Name.StartsWith("lbl") Then MyToolTip.SetToolTip(c, c.Text)
        Next
        For Each c As Control In grpStatus.Controls
            If c.Name.StartsWith("lbl") Then MyToolTip.SetToolTip(c, c.Text)
        Next
        MyToolTip.SetToolTip(picBlackInk, lblBlack.Text)
        MyToolTip.SetToolTip(picCMYInk, lblColor.Text)
    End Sub 'Set ToolTips (in case some text can't fit entirely into its control)

    Private Sub btnSendJob_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnSendJob.Click
        If chkJPEG.Checked AndAlso MsgBox("Report and letter printing for JPEG discs is " & IIf(chkDisablePrinting.Checked, "OFF.", "ON.") & vbCrLf & _
                                           "Would you like to " & IIf(chkDisablePrinting.Checked, "enable", "disable") & " printing?", MsgBoxStyle.Question + MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
            chkDisablePrinting.Checked = Not chkDisablePrinting.Checked
        End If

        Try 'Delete any ERR files that might exist.
            Kill(SharedJobFolder & "\*.ERR")
        Catch ex As Exception
        End Try

        FinishedCDJobs = 0
        FinishedDVDJobs = 0
        FailedCDJobs = 0
        FailedDVDJobs = 0
        CurrentJobs = New List(Of Job)
        FailedJobs = New List(Of Job)

        Dim AlreadyWorking As Boolean = False 'Is the job in the queue already being worked on?
        Dim NumJPEGJobs As Integer = GetNumInQueue(DiscTypeEnum.CD) 'a return value of -1 indicates an error
        Dim NumTIFJobs As Integer = GetNumInQueue(DiscTypeEnum.DVD) 'a return value of -1 indicates an error
        WriteLog(IIf(chkJPEG.Checked And chkTIF.Checked, "JPEG and TIF", IIf(chkJPEG.Checked, "JPEG", "TIF")) & " jobs were started.", txtLog)

        'Disable some controls.
        SetSettingControls(False)

        'Delete the status file if it already exists;
        'this prevents the status file from growing too large.
        If FileSystem.FileExists(SharedJobFolder & "Status\PTStatus.txt") Then FileSystem.DeleteFile(SharedJobFolder & "Status\PTStatus.txt")

        '################# MAIN LOOP ##################
        Do While IIf(chkJPEG.Checked, NumJPEGJobs, 0) + IIf(chkTIF.Checked, NumTIFJobs, 0) > 0 AndAlso FailedJobs.Count < IIf(chkJPEG.Checked, NumJPEGJobs, 0) + IIf(chkTIF.Checked, NumTIFJobs, 0) 'There are still jobs in the queue.

            'Show the number of jobs left in the queue.
            lblCDLeft.Text = NumJPEGJobs
            lblDVDLeft.Text = NumTIFJobs
            'There was an error retrieving information from the DB; close down.
            If NumJPEGJobs = -1 Or NumTIFJobs = -1 Then
                WriteLog("An error occurred while connecting with the database. CW will shut down.")
                MsgBox("There has been an unexpected error communicating with the database. The application will exit.", MsgBoxStyle.OkOnly, "Fatal DB Error")
                End
            End If

            For DT As DiscTypeEnum = 0 To 1
                'If discs of type DT are included, do a job of type DT.
                If IIf(DT = DiscTypeEnum.CD, chkJPEG.Checked, chkTIF.Checked) AndAlso IIf(DT = DiscTypeEnum.CD, NumJPEGJobs, NumTIFJobs) > 0 AndAlso TotalCurrentJobs < 2 Then
                    If DT = DiscTypeEnum.CD AndAlso CurrentJobs.Count = 1 AndAlso CurrentJobs(0).DiscType = "CD" AndAlso chkTIF.Checked AndAlso NumTIFJobs > 0 Then
                        'We are working on 1 CD: one drive is open. If there are DVD's needed also, 
                        'skip the CD cycle and put a DVD in the available drive.
                    Else
                        Dim Row As Integer = 0
                        Do
                            AlreadyWorking = False
                            Dim QRetVal As ReadQueueVal = ReadQueue(IIf(DT = DiscTypeEnum.CD, "CD", "DVD"), Row)
                            If QRetVal = ReadQueueVal.ReadOK AndAlso GetPrintInfo() Then
                                For Each j As Job In CurrentJobs 'Check LastQJob's XNumber against all Current Jobs
                                    If j.XNumber = LastQJob(SUB_PlateNO) Then AlreadyWorking = True
                                Next
                                For Each ChkFail As Job In FailedJobs
                                    If ChkFail.XNumber = LastQJob(SUB_PlateNO) And ChkFail.DiscType = LastQJob(SUB_DiscType) Then AlreadyWorking = True
                                Next
                            ElseIf QRetVal = ReadQueueVal.QueueEmpty Then
                                Exit For
                            Else
                                WriteLog("Process aborted because queue could not be read.", txtLog)
                                MsgBox("Job information could not be obtained from the database. Please contact your database administrator.", MsgBoxStyle.OkOnly, "Database Error")
                                SetSettingControls(True)
                                Exit Sub
                            End If
                            Row += 1
                            If Row > 1 And AlreadyWorking Then Exit For
                        Loop While AlreadyWorking
                        If Not (chkReportsOnly.Checked OrElse chkLettersOnly.Checked) Then                            
                            BurnDisc(DT, NextAvailableJobNo(), LastQJob(SUB_PlateNO), LastPrintInfo(SUB_FirstName), LastPrintInfo(SUB_LastName), LastPrintInfo(SUB_SampleInfo), LastPrintInfo(SUB_SetupDate))
                        Else
                            Dim FakeJob As New Job
                            FakeJob.FakeDiscJob(NextAvailableJobNo(), DT, LastQJob(SUB_PlateNO))
                        End If
                    End If
                End If
            Next

            'Re-figure the number of jobs in the queue.
            NumJPEGJobs = GetNumInQueue(DiscTypeEnum.CD)
            NumTIFJobs = GetNumInQueue(DiscTypeEnum.DVD)
            lblCDLeft.Text = NumJPEGJobs
            lblDVDLeft.Text = NumTIFJobs

            'Show progress made thus far.
            With prgSendJob
                .Minimum = 0
                .Maximum = NumJPEGJobs + NumTIFJobs
                .Value = FinishedCDJobs + FinishedDVDJobs
            End With

            'We can't do anything if there are already 2 jobs in progress.
            'DiscTimer's "Tick" event handler will stop this loop.
            Do While CurrentJobs.Count = 2 OrElse IIf(chkJPEG.Checked, NumJPEGJobs, 0) + IIf(chkTIF.Checked, NumTIFJobs, 0) = 1 And CurrentJobs.Count = 1
                Application.DoEvents()
            Loop
            Application.DoEvents()

        Loop
        '################# MAIN LOOP ##################

        Dim FailList As String = ""
        If FailedJobs.Count > 0 Then
            FailList = vbCrLf & vbCrLf & FailedJobs.Count & " job(s) failed to complete: "
            For Each fj As Job In FailedJobs
                FailList &= vbCrLf & " -" & fj.DiscType & " " & fj.XNumber
            Next
        End If
        WriteLog("The queue is empty. All jobs have been completed.", txtLog)
        MsgBox("Finished burning jobs; the queue is empty." & FailList, MsgBoxStyle.OkOnly, "Empty Queue")
        Job.PrintAllLetters()
        WriteLog("All operations have completed.", txtLog)
        'Enable some controls.
        SetSettingControls(True)

        'Reset some data from the last session.
        Job.Contacts.Clear()
        Array.Clear(Job.NumCurrentJobs, 0, Job.NumCurrentJobs.Length)
        Array.Clear(Job.NumFailedJobs, 0, Job.NumFailedJobs.Length)
        Array.Clear(Job.NumFinishedJobs, 0, Job.NumFinishedJobs.Length)

    End Sub 'Begins sending jobs to the SharedJobFolder. Contains MAIN LOOP.

    Private Sub SetSettingControls(ByVal ToValue As Boolean)
        btnSendJob.Enabled = ToValue
        txtJPEGSource.Enabled = ToValue
        btnBrowseJPEGSource.Enabled = ToValue
        chkJPEG.Enabled = ToValue
        txtTIFSource.Enabled = ToValue
        btnBrowseTIFSource.Enabled = ToValue
        chkTIF.Enabled = ToValue
        StatusTimer.Enabled = Not ToValue
    End Sub '(En/dis)ables controls that can't be used after 'Start' is clicked.

    Private Sub mnuEditQueue_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuEditQueue.Click
        WriteLog("Queue editing started...")
        frmEditQueue.ShowDialog()
        WriteLog("Queue editing finished...")
    End Sub 'Shows dialog for removing items from the DB queue.

    Private Sub mnuTechHelp_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuTechHelp.Click
        frmTechHelp.ShowDialog()
    End Sub 'Shows dialog that displays "readme.txt".

    Private Sub mnuAfterCurrent_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuAfterCurrent.Click
        SendPTMessage("SHUTDOWN_AFTERJOB")
        WriteLog("CrystalWriter is shutting down. The Bravo XRP will shut down after current jobs are finished.", txtLog)
        End
    End Sub 'Shuts down CW immediately and the XRP after current jobs are finished.

    Private Sub mnuImmediately_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuImmediately.Click
        If MsgBox("You should only shut down immediately in case of an emergency." & vbCrLf & _
                  "The system will be left in an unknown state. This may complicate the process of restarting the Bravo XRP." & vbCrLf & _
                  "Do you still want to proceed?", MsgBoxStyle.Exclamation + MsgBoxStyle.YesNo, "WARNING!") = MsgBoxResult.Yes Then
            SendPTMessage("SHUTDOWN_IMMEDIATE")
            WriteLog("CrystalWriter and the Bravo XRP are shutting down...", txtLog)
            End
        End If
    End Sub 'Shuts down both CW and the XRP immediately.

    Private Sub mnuJob1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuJob1.Click
        SendPTMessage("ABORT", CurrentJobs(0).JobID)
        WriteLog("Job 1 was aborted.", txtLog)
    End Sub 'Aborts Job 1

    Private Sub mnuJob2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuJob2.Click
        SendPTMessage("ABORT", CurrentJobs(1).JobID)
        WriteLog("Job 2 was aborted.", txtLog)
    End Sub 'Aborts Job 2

    Private Sub mnuRefreshBins_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuRefreshBins.Click
        SendPTMessage("CHECK_DISCSINBIN")
    End Sub 'Checks to see how many discs are in each bin.

    Private Sub mnuPrintReport_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuPrintReport.Click
        Dim SampleJob As New Job(0, DiscTypeEnum.CD, "X999999999", "")
        SampleJob.PrintReport()
    End Sub 'Prints a sample report with garbage data.

    Private Sub mnuPrintLetter_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuPrintLetter.Click
        Dim SampleJob As New Job(0, DiscTypeEnum.CD, "X999999999", "")
        Dim OtherSampleJob As New Job(-1, DiscTypeEnum.CD, "X999999999", "")
        Job.PrintAllLetters()
    End Sub 'Prints a sample letter with garbage data.

    Public Sub CloseCrystalWriter()
        Try
            crystalwriter_PT.WordPrint.ExitWord()
            WriteLog("MS Word was closed.", txtLog)
        Catch ex As Exception
            WriteLog("Could not close MS Word: " & ex.Message, txtLog)
            Me.Dispose()
        End Try
        End
    End Sub 'Exits MS Word and shuts CrystalWriter down cleanly.

    Private Sub mnuChangeDB_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles mnuChangeDB.Click
        dlgChangeDB.ShowDialog()
        If dlgChangeDB.DialogResult = Windows.Forms.DialogResult.OK Then
            Do While (Not Connect2DB())
                MsgBox("Could not connect to the database.", MsgBoxStyle.OkOnly, "Unable to connect")
                If MsgBox("Would you like to change the connection?", MsgBoxStyle.YesNo, "Database Connection") = MsgBoxResult.No Then
                    End
                End If
                dlgChangeDB.ShowDialog()
                If dlgChangeDB.DialogResult <> Windows.Forms.DialogResult.OK Then End
            Loop
        End If
    End Sub 'Changes the database connection.

    Private Sub chkReportsOnly_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkReportsOnly.CheckedChanged
        If chkReportsOnly.Checked Then chkDisablePrinting.Checked = False
    End Sub 'If Checked, don't do any discs, but print their reports.

    Private Sub chkLettersOnly_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkLettersOnly.CheckedChanged
        If chkLettersOnly.Checked Then chkDisablePrinting.Checked = False
    End Sub 'If Checked, don't do any discs, but print the letters.

    Private Sub chkDisablePrinting_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkDisablePrinting.CheckedChanged
        If chkDisablePrinting.Checked Then
            chkReportsOnly.Checked = False
            chkLettersOnly.Checked = False
        End If
    End Sub 'If Checked, do all discs, but no reports or letters.

End Class
