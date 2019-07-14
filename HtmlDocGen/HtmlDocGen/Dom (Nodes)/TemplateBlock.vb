Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.IO
Imports System.Linq

Public MustInherit Class TemplateBlock
  Inherits DocumentBlock

#Region " Childs "

  Private _Childs As New Dictionary(Of String, List(Of DocumentBlock))

  <Obsolete("Please specify the areaId via index param: Childs('myAreaId')"), EditorBrowsable(EditorBrowsableState.Never)>
  Public Overridable ReadOnly Property Childs() As IList(Of DocumentBlock)
    Get
      Return Me.Childs(String.Empty)
    End Get
  End Property

  Public ReadOnly Property Childs(areaId As String) As IList(Of DocumentBlock)
    Get
      areaId = areaId.Trim().ToLower()
      If (Not _Childs.ContainsKey(areaId)) Then
        _Childs.Add(areaId, New List(Of DocumentBlock))
      End If
      Return _Childs(areaId)
    End Get
  End Property

  Protected Function GetRegisteredAreaIds() As String()
    Return _Childs.Keys.ToArray()
  End Function

#End Region

  Protected Overridable Function OpenTemplateStream(scope As String, formatProvider As IDocumentFormatProvider) As Stream
    Return formatProvider.OpenTemplateStream(scope, Me.GetType)
  End Function


#Region " Generate "

  Protected Overridable Sub PickSources(dataSourceGetter As Func(Of String, Object))
  End Sub

  Public Overrides Sub WriteOutputTo(target As StreamWriter, provider As IDocumentFormatProvider, dataSourceGetter As Func(Of String, Object), scope As String)

    Dim wrappeddataSourceGetter = (
      Function(name As String) As Object
        Dim targetName As String = name.SubStringBefore(".")
        Dim memberName As String = name.SubStringAfter(".")
        Dim dataSource As Object

        'new way with named areas
        For Each areaId In Me.GetRegisteredAreaIds()
          If (areaId.ToLower() = targetName.ToLower()) Then
            Return Me.Childs(targetName)
          End If
        Next

        'obsolete way
        If (targetName.ToLower() = "subparts") Then
          Return Me.Childs(memberName)

        ElseIf (targetName.ToLower() = "me") Then
          dataSource = Me
        Else
          'targetName seems to be a class-name
          dataSource = Me.DataSource.GetSource(targetName)
        End If

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

    Me.PickSources(dataSourceGetter)

    If (Me.IsVisible(wrappeddataSourceGetter)) Then
      provider.WriteTemplateToStream(scope, target, wrappeddataSourceGetter, Me.GetType, Me.OpenTemplateStream(scope, provider))
    End If

  End Sub

#End Region

End Class
