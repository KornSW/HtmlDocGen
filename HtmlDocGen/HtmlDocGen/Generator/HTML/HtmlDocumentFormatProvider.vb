Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Text

Public Class HtmlDocumentFormatProvider
  Implements IDocumentFormatProvider

  Public Sub WriteImageToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), source As Image) Implements IDocumentFormatProvider.WriteImageToStream
    Me.WriteObjectToStream(scope, target, dataSourceGetter, source, False)
  End Sub

  Public Sub WriteTemplateToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), forTarget As Type, templateStream As Stream) Implements IDocumentFormatProvider.WriteTemplateToStream
    Me.WriteStreamToStream(scope, target, dataSourceGetter, templateStream, True)
    'Me.WriteStreamToStream(scope, target, dataSourceGetter, Me.OpenTemplateStream(scope, forTarget), True)
  End Sub

  Public Sub WriteStreamToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), source As Stream, resolvePlaceholders As Boolean) Implements IDocumentFormatProvider.WriteStreamToStream
    If (resolvePlaceholders) Then
      Using sr As New StreamReader(source)
        Me.WriteTextToStream(scope, target, dataSourceGetter, sr)
      End Using
    Else
      source.CopyTo(target.BaseStream)
    End If
  End Sub

  Public Sub WriteTextToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), text As String) Implements IDocumentFormatProvider.WriteTextToStream
    Using sr As New StringReader(text)
      Me.WriteTextToStream(scope, target, dataSourceGetter, sr)
    End Using
  End Sub

  Public Sub WriteTextToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), textReader As TextReader)

    Dim placeholderProcessingMethod =
      Sub(placeHolderString As String)
        Dim placeHolderObject As Object
        Dim formatString = placeHolderString.SubStringAfter(":").NothingIfEmpty()
        Dim objectName = placeHolderString.SubStringBefore(":")
        Dim newScope As String = scope
        If (objectName.Contains("~")) Then
          newScope = objectName.SubStringAfter("~")
          objectName = objectName.SubStringBefore("~")
        End If
        placeHolderObject = dataSourceGetter.Invoke(objectName)
        Me.WriteObjectToStream(newScope, target, dataSourceGetter, placeHolderObject, False, formatString)
      End Sub

    Dim contentProcessingMethod =
      Sub(placeHolderString As String)
        Me.WriteObjectToStream(scope, target, dataSourceGetter, placeHolderString, True)
      End Sub

    textReader.Tokenize("<!--{", "}-->", placeholderProcessingMethod, contentProcessingMethod)

  End Sub

  Public Sub WriteObjectToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), obj As Object, isTemplateContent As Boolean, Optional formatString As String = Nothing) Implements IDocumentFormatProvider.WriteObjectToStream

    If (obj Is Nothing) Then 'NOTHING
      Exit Sub

    ElseIf (TypeOf (obj) Is String) Then 'STRING
      If (Not String.IsNullOrEmpty(DirectCast(obj, String))) Then
        If (isTemplateContent) Then
          target.Write(DirectCast(obj, String))
        Else
          target.Write(Me.EscapeHtml(DirectCast(obj, String)))
        End If
      End If

    ElseIf (GetType(DocumentBlock).IsAssignableFrom(obj.GetType())) Then 'SUB-DOCUMENT
      DirectCast(obj, DocumentBlock).WriteOutputTo(target, Me, dataSourceGetter, scope)

    ElseIf (TypeOf (obj) Is Image) Then 'IMAGE
      'target.Write("<img src=""data:image/png;base64,")
      target.Write("data:image/png;base64,")
      target.Write(DirectCast(obj, Image).GetBytes(System.Drawing.Imaging.ImageFormat.Png).ToBase64String())
      'target.Write(""" />")

    ElseIf (TypeOf (obj) Is Stream) Then 'STREAM (a datasource could inject a file)
      Me.WriteStreamToStream(scope, target, dataSourceGetter, DirectCast(obj, Stream), False)

    ElseIf (GetType(System.Collections.IEnumerable).IsAssignableFrom(obj.GetType())) Then
      For Each item In DirectCast(obj, System.Collections.IEnumerable)
        If (TypeOf (item) Is DocumentBlock) Then
          'wenn models properties vom typ DocumentBlock enthalten...
          'eigentlich ein sehr schlechtes design!!!
          DirectCast(item, DocumentBlock).WriteOutputTo(target, Me, dataSourceGetter, scope)
        Else
          Me.WriteObjectToStream(scope, target, dataSourceGetter, item, isTemplateContent, formatString)
        End If
      Next

    Else 'OTHER TYPES (using ToString() with Format)
      Dim str As String
      If (String.IsNullOrWhiteSpace(formatString)) Then
        str = obj.ToString()
      Else
        str = String.Format("{0:" & formatString & "}", obj)
      End If
      If (isTemplateContent) Then
        target.Write(str)
      Else
        target.Write(Me.EscapeHtml(str))
      End If
    End If
  End Sub

  Public Sub WritePagebreakToStream(scope As String, target As StreamWriter) Implements IDocumentFormatProvider.WritePagebreakToStream
    target.WriteLine("<p id=""pagebreak"">")
  End Sub

  Private Function EscapeHtml(text As String) As String
    Return System.Web.HttpUtility.HtmlEncode(text).Replace(" ", "&nbsp;").Replace(Environment.NewLine, "<br />")
  End Function

  Public Sub WriteTextToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object), formatstring As String, ParamArray args() As Object) Implements IDocumentFormatProvider.WriteTextToStream
    Me.WriteTextToStream(scope, target, dataSourceGetter, String.Format(formatstring, args))
  End Sub

  Public Property TemplateDirectory As String Implements IDocumentFormatProvider.TemplateDirectory

  Public Function OpenTemplateStream(scope As String, forTarget As Type) As Stream Implements IDocumentFormatProvider.OpenTemplateStream
    Dim file As EmbeddedFile
    Dim fileName As String

    If (scope Is Nothing) Then
      fileName = String.Format("{0}.Template.htm", forTarget.Name)
    Else
      fileName = String.Format("{0}.Template.{1}", forTarget.Name, scope)
    End If

    file = forTarget.Assembly.EmbeddedFiles.File(fileName)

    If (file Is Nothing) Then
      Throw New ApplicationException("Template file '{1}' was not found as Embedded Resource in Assembly '{0}'".FormatWith(forTarget.Assembly.GetName.Name, fileName))
    End If

    Return file.OpenStream()
  End Function

  Public Sub WriteDocumentStartToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object)) Implements IDocumentFormatProvider.WriteDocumentStartToStream
    'target.WriteLine("<html>")
    'target.WriteLine("<head>")
    'target.WriteLine("<style>")
    ''Using s = Me.OpenTemplateStream("DefaultCssStyle.css")
    ''  target.WriteLine(s.ReadAllText())
    ''End Using
    'target.WriteLine("</style>")
    'target.WriteLine("</head>")
    'target.WriteLine("<body>")
  End Sub

  Public Sub WriteDocumentEndToStream(scope As String, target As StreamWriter, dataSourceGetter As Func(Of String, Object)) Implements IDocumentFormatProvider.WriteDocumentEndToStream
    'target.WriteLine("</body>")
    'target.WriteLine("</html>")
  End Sub


End Class
