Imports System
Imports System.Drawing
Imports System.IO

Public Interface IDocumentFormatProvider

  Function OpenTemplateStream(scope As String, forTarget As Type) As Stream

  Property TemplateDirectory As String

  Sub WriteTemplateToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), forTarget As Type, templateStream As Stream)
  Sub WriteStreamToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), source As Stream, resolvePlaceholders As Boolean)
  Sub WriteTextToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), text As String)
  Sub WriteTextToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), formatstring As String, ParamArray args() As Object)
  Sub WriteImageToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), source As Image)
  Sub WritePagebreakToStream(scope As String, target As StreamWriter)

  Sub WriteObjectToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), obj As Object, isTemplateContent As Boolean, Optional formatString As String = Nothing)

  Sub WriteDocumentStartToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object))
  Sub WriteDocumentEndToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object))

End Interface

