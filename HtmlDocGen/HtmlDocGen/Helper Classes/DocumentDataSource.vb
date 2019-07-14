Imports System
Imports System.Collections.Generic

Public Class DocumentDataSource

  Public Sub New()
  End Sub

  Private _Local As New Dictionary(Of String, Object)
  Public Sub SetSource(source As Object)
    Dim typeKey = source.GetType.Name
    If (_Local.ContainsKey(typeKey)) Then
      _Local(typeKey) = source
    Else
      _Local.Add(typeKey, source)
    End If

  End Sub

  Public Sub SetSource(Of T)(source As T)
    Dim typeKey = GetType(T).Name
    If (_Local.ContainsKey(typeKey)) Then
      _Local(typeKey) = source
    Else
      _Local.Add(typeKey, source)
    End If
  End Sub

  Public Function GetSource(Of T)() As T
    Return DirectCast(Me.GetSource(GetType(T)), T)
  End Function

  Public Sub CopyTo(target As DocumentDataSource)
    For Each localKey In _Local.Keys
      If (target._Local.ContainsKey(localKey)) Then
        target._Local(localKey) = _Local(localKey)
      Else
        target._Local.Add(localKey, _Local(localKey))
      End If
    Next
  End Sub

  Public Function GetOrAddSource(Of T As New)() As T
    Dim instance = Me.GetSource(Of T)()
    If (instance Is Nothing) Then
      instance = New T
      Me.SetSource(instance)
    End If
    Return instance
  End Function

  Public Function GetSource(ofType As Type) As Object
    Return Me.GetSource(ofType.Name)
  End Function

  Public Function GetSource(ofTypeName As String) As Object
    If (_Local.ContainsKey(ofTypeName)) Then
      Return _Local(ofTypeName)
    Else
      Return Nothing
    End If
  End Function

End Class
