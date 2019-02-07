Imports System.IO
Imports System.Text

Public Class HtmlDocumentGenerator
  Inherits DocumentGenerator

  Public Sub New()
    Me.DocumentFormatProvider = New HtmlDocumentFormatProvider
  End Sub

  Protected Overrides ReadOnly Property DefaultScope As String
    Get
      Return "htm"
    End Get
  End Property

End Class
