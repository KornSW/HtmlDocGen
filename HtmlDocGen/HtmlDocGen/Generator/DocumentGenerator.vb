Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text

Public Class DocumentGenerator

  Public Sub New()
  End Sub

  Public Sub GenerateDocument(target As StreamWriter, ParamArray rootNodes() As DocumentBlock)
    Me.GenerateDocument(target, rootNodes.ToList())
  End Sub

  Protected Overridable ReadOnly Property DefaultScope As String
    Get
      Return Nothing
    End Get
  End Property

  Public Sub GenerateDocument(target As StreamWriter, rootNodes As IEnumerable(Of DocumentBlock), Optional scope As String = Nothing)

    'TODO:
    'For Each node In rootNodes
    '  node.RegisterStylesTo(Me.DocumentFormatProvider)
    'Next

    If (scope Is Nothing) Then
      scope = Me.DefaultScope
    End If

    Me.DocumentFormatProvider.WriteDocumentStartToStream(scope, target, AddressOf Me.ResolveGeneratorSpecificPlaceholders)
    For Each node In rootNodes
      node.WriteOutputTo(target, Me.DocumentFormatProvider, AddressOf Me.ResolveGeneratorSpecificPlaceholders, scope)
    Next
    Me.DocumentFormatProvider.WriteDocumentEndToStream(scope, target, AddressOf Me.ResolveGeneratorSpecificPlaceholders)
    target.Flush()
  End Sub

  Private Function ResolveGeneratorSpecificPlaceholders(name As String) As Object
    Select Case name
      Case "Generator.FormatProviderType" : Return Me.DocumentFormatProvider.GetType().Name
      Case "DateTime.Now" : Return DateTime.Now
      Case "Environment.MachineName" : Return Environment.MachineName
      Case "Environment.UserName" : Return Environment.UserName
      Case "Guid.NewGuid" : Return Guid.NewGuid.ToString()
      Case Else
        Dim typeName As String = name.SubStringBefore(".")
        Dim t = Type.GetType(typeName, False)
        If (t IsNot Nothing) Then
          Return t.GetValueFrom(Nothing, name.Substring(typeName.Length + 1, name.Length - typeName.Length - 1))
        End If
    End Select
    Return Nothing
  End Function

  Public Property DocumentFormatProvider As IDocumentFormatProvider

End Class
