Imports System.ComponentModel
Imports System.IO
Imports System.Reflection
Imports System.Runtime.CompilerServices

Public Class Content

  Private _Data As Object()

  Public Sub New(ParamArray data() As Object)
    _Data = data
  End Sub

  Public ReadOnly Property Data As Object
    Get
      Return _Data(0)
    End Get
  End Property

  Public ReadOnly Property Data(index As Integer) As Object
    Get
      Return _Data(index)
    End Get
  End Property

End Class
