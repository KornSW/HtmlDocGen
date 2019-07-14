Imports System
Imports System.Collections
Imports System.IO

Public Delegate Function DataSourceGetterMethod(typeNameAndProperty As String) As Object

Public Class MultipleDocumentBlocks(Of TNode As {TemplateBlock, New})
  Inherits DocumentBlock

  Private _Selector As Func(Of DataSourceGetterMethod, IEnumerable)
  Public Sub New(iteratableDataSource As IEnumerable)
    _Selector = Function(ds) iteratableDataSource
  End Sub

  Public Sub New(selector As Func(Of DataSourceGetterMethod, IEnumerable))
    _Selector = selector
  End Sub

  Public Overrides Sub WriteOutputTo(target As StreamWriter, provider As IDocumentFormatProvider, dataSourceGetter As Func(Of String, Object), scope As String)

    Dim wrappeddataSourceGetter = (
      Function(name As String) As Object
        Dim className As String = name.SubStringBefore(".")
        Dim memberName As String = name.SubStringAfter(".")

        Dim dataSource As Object = Me.DataSource.GetSource(className)
        If (dataSource Is Nothing) Then
          If (dataSourceGetter IsNot Nothing) Then
            Return dataSourceGetter.Invoke(name)
          Else
            Return Nothing
          End If
        Else
          If (memberName = String.Empty) Then
            Return dataSource
          Else
            Return dataSource.GetType().GetValueFrom(dataSource, memberName)
          End If
        End If

      End Function
    )

    If (Me.IsVisible(wrappeddataSourceGetter)) Then

      Dim currentNode As New TNode
      Dim source As IEnumerable
      source = _Selector.Invoke(wrappeddataSourceGetter)

      For Each iteratingDataSource As Object In source
        currentNode.DataSource.SetSource(iteratingDataSource)
        currentNode.WriteOutputTo(target, provider, wrappeddataSourceGetter, scope)
      Next

    End If

  End Sub

End Class
