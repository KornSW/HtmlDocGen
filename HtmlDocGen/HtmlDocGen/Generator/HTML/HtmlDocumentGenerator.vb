Imports System.IO
Imports System.Text

Public Class HtmlDocumentGenerator
  Inherits DocumentGenerator

  Public Sub New(Optional enableTokenizingV2 As Boolean = False)
    Me.DocumentFormatProvider = New HtmlDocumentFormatProvider With {.EnableTokenizingV2 = enableTokenizingV2}
  End Sub

  Protected Overrides ReadOnly Property DefaultScope As String
    Get
      Return "htm"
    End Get
  End Property

End Class
