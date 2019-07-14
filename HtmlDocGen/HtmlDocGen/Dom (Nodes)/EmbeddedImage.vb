Imports System
Imports System.Drawing
Imports System.IO

Public MustInherit Class EmbeddedImage
  Inherits DocumentBlock

  Protected MustOverride Function GenerateImage(dataSourceGetter As Func(Of String, Object)) As Image

  Public Overrides Sub WriteOutputTo(target As StreamWriter, provider As IDocumentFormatProvider, dataSourceGetter As Func(Of String, Object), scope As String)
    provider.WriteImageToStream(scope, target, dataSourceGetter, Me.GenerateImage(dataSourceGetter))
  End Sub

End Class

Public Class StaticEmbeddedImage
  Inherits EmbeddedImage

  Private _Image As Image

  Public Sub New(image As Image)
    _Image = image
  End Sub

  Protected Overrides Function GenerateImage(dataSourceGetter As Func(Of String, Object)) As Image
    Return _Image
  End Function

End Class
