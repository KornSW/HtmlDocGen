Imports System
Imports System.IO

Public MustInherit Class DocumentBlock

  Public Sub New()
  End Sub

  Private _DataSource As New DocumentDataSource

  Public ReadOnly Property DataSource As DocumentDataSource
    Get
      Return _DataSource
    End Get
  End Property

  Protected Overridable Function IsVisible(dataSourceGetter As Func(Of String, Object)) As Boolean
    Return True
  End Function

  Public MustOverride Sub WriteOutputTo(target As StreamWriter, provider As IDocumentFormatProvider, dataSourceGetter As Func(Of String, Object), scope As String)

End Class
