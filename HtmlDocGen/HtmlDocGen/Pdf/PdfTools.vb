Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Runtime.InteropServices
Imports System.Drawing

Public Class PdfTools

#Region " Path Configuration "

  Private Shared _BinariesDir As String = Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
  Private Sub New()
  End Sub

  Public Shared Property BinariesDir As String
    Get
      Return _BinariesDir
    End Get
    Set(value As String)
      _BinariesDir = value
    End Set
  End Property

  Public Shared Property ExecutableTimeoutMinutes As Integer = 5

  Private Shared ReadOnly Property WkhtmlToPdfExecutablePath As String
    Get
      Return Path.Combine(_BinariesDir, "wkhtmltopdf.exe")
    End Get
  End Property

#End Region

  Public Shared Function RunWkHtmlToPdf(pdfFile As String, topMargin As Integer, bottomMargin As Integer, htmlFile As String) As Integer
    Try

      Dim outputReceiver As New StringBuilder
      Dim processExitCode As Integer
      Dim args As New StringBuilder

      With args
        .Append("-L 0")
        .Append(" -R 0")

        If (bottomMargin >= 0) Then
          .AppendFormat(" -B {0}", bottomMargin)
        End If
        If (topMargin >= 0) Then
          .AppendFormat(" -T {0}", topMargin)
        End If

        .Append(" --disable-smart-shrinking")
        '.Append(" --image-quality 100")

        'BUGFIX
        Dim scaling = GetWindowsScaling()
        If (scaling <> 1D) Then
          .AppendFormat(" --zoom {0:0.0}", scaling)
        End If

        If (htmlFile.Contains(" ")) Then
          .AppendFormat(" ""{0}""", htmlFile)
        Else
          .AppendFormat(" {0}", htmlFile)
        End If
        If (pdfFile.Contains(" ")) Then
          .AppendFormat(" ""{0}""", pdfFile)
        Else
          .AppendFormat(" {0}", pdfFile)
        End If

        ' "--header-html  '"--footer-html
      End With

      Dim commandlineArguments = args.ToString()
      processExitCode = RunCommandLineExe(WkhtmlToPdfExecutablePath, commandlineArguments, Sub(s) outputReceiver.Append(s))

      Select Case processExitCode
        Case 0 'success
        Case 1 'success ???
        Case Else
          If (outputReceiver.Length < 2) Then
            Throw New ApplicationException(String.Format("Process returned exit code {0}!", processExitCode))
          Else
            Throw New ApplicationException(String.Format("Process returned exit code {0}! Console output was: ", processExitCode, outputReceiver))
          End If
      End Select

      Return processExitCode
    Catch ex As Exception
      Throw New ApplicationException(String.Format("Error executing '{0}': {1}", Path.GetFileName(WkhtmlToPdfExecutablePath), ex.Message), ex)
    End Try
  End Function

#Region " Helper "

  Public Shared Function RunCommandLineExe(executable As String, arguments As String, stdOutReceiver As Action(Of String)) As Integer

    Dim startInfo As New ProcessStartInfo
    With startInfo
      .UseShellExecute = False
      .RedirectStandardOutput = True
      .FileName = executable
      .Arguments = arguments
      ' .RedirectStandardError = True
      ' .RedirectStandardInput = True
      .WindowStyle = ProcessWindowStyle.Hidden
      .CreateNoWindow = True
    End With

    Dim timeoutMinutes As Integer = ExecutableTimeoutMinutes
    Dim timeoutDeadline As DateTime
    If (timeoutMinutes < 1) Then
      timeoutMinutes = 0
    End If
    timeoutDeadline = DateTime.Now.AddMinutes(timeoutMinutes)

    Dim processHandle = System.Diagnostics.Process.Start(startInfo)

    While (Not processHandle.HasExited)
      If (processHandle.StandardOutput.Peek() > 0) Then
        stdOutReceiver.Invoke(processHandle.StandardOutput.ReadToEnd())
      Else
        Threading.Thread.Sleep(10)
      End If
      If (DateTime.Now > timeoutDeadline) Then
        Throw New Exception("Timeout of {0} minutes has been reached")
      End If
      processHandle.Refresh()
    End While

    Return processHandle.ExitCode
  End Function

  'http://www.pinvoke.net/default.aspx/gdi32/GetDeviceCaps.html
  Private Const LOGPIXELSX As Integer = 88
  Private Const LOGPIXELSY As Integer = 90
  Private Const ASPECTX As Integer = 40
  Private Const SCALINGFACTORX As Integer = 114
  Private Const SCALINGFACTORY As Integer = 115
  Private Const VERTRES As Integer = 10
  Private Const DESKTOPVERTRES As Integer = 117

  Private Shared Function GetWindowsScaling() As Decimal
    Dim result As Decimal = 1
    Dim desktopHandle As IntPtr
    Try
      desktopHandle = GetDC(IntPtr.Zero)
      Dim LogicalScreenHeight As Integer = GetDeviceCaps(desktopHandle, VERTRES)
      Dim PhysicalScreenHeight As Integer = GetDeviceCaps(desktopHandle, DESKTOPVERTRES)
      result = CDec(CType(PhysicalScreenHeight, Single) / CType(LogicalScreenHeight, Single))
    Catch
    Finally
      Try
        ReleaseDC(IntPtr.Zero, desktopHandle)
      Catch
      End Try
    End Try
    Return result
  End Function

  <DllImport("gdi32.dll")> _
  Private Shared Function GetDeviceCaps(hdc As IntPtr, nIndex As Integer) As Integer
  End Function

  <DllImport("user32.dll")> _
  Private Shared Function GetDC(hWnd As IntPtr) As IntPtr
  End Function

  <DllImport("user32.dll")> _
  Private Shared Function ReleaseDC(hWnd As IntPtr, hDC As IntPtr) As Integer
  End Function

#End Region

End Class
