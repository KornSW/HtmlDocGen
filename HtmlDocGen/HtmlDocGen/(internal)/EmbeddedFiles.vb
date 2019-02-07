Imports System
Imports System.Linq
Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Diagnostics

Namespace My.Resources

  Friend Class MyEmbeddedFileRepository
    Inherits EmbeddedFileRepository

    Public Sub New()
      MyBase.New(Assembly.GetExecutingAssembly())
    End Sub

  End Class

#Region " .My-Extension "

  <Microsoft.VisualBasic.HideModuleName>
  Friend Module EmbeddedFilesMyExtension

    Private _EmbeddedFiles As MyEmbeddedFileRepository = Nothing

    Public ReadOnly Property EmbeddedFiles As MyEmbeddedFileRepository
      Get
        If (_EmbeddedFiles Is Nothing) Then
          _EmbeddedFiles = New MyEmbeddedFileRepository()
        End If
        Return _EmbeddedFiles
      End Get
    End Property

  End Module

#End Region

End Namespace

Friend Class EmbeddedFileRepository

#Region " Declarations & Constructor "

  <DebuggerBrowsable(DebuggerBrowsableState.Never)> Private _Files As IEnumerable(Of EmbeddedFile) = Nothing
  <DebuggerBrowsable(DebuggerBrowsableState.Never)> Private _Assembly As Assembly

  Public Sub New(assemblyFilename As String)
    Me.New(Assembly.LoadFile(assemblyFilename))
  End Sub

  Public Sub New(assembly As Assembly)
    _Assembly = assembly
  End Sub

#End Region

#Region " Enumeration (Shared) "

  Private Shared Function EnumerateResourceFilesFrom(sourceAssembly As Assembly) As IEnumerable(Of EmbeddedFile)
    Dim files As New List(Of EmbeddedFile)
    Dim ns = sourceAssembly.GetDefaultNamespace()

    For Each fullFileName As String In sourceAssembly.GetManifestResourceNames()
      If (Not fullFileName.ToLower().EndsWith(".resources") AndAlso
          Not fullFileName.ToLower().EndsWith(".resx")) Then
        files.Add(New EmbeddedFile(fullFileName, sourceAssembly, ns))
      End If
    Next

    Return files
  End Function

#End Region

#Region " Repository Access "

  <DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>
  Public Overloads ReadOnly Property Files As IEnumerable(Of EmbeddedFile)
    Get
      If (_Files Is Nothing) Then
        _Files = EnumerateResourceFilesFrom(_Assembly)
      End If

      Return _Files
    End Get
  End Property

  <DebuggerBrowsable(DebuggerBrowsableState.Never)>
  Default Public Overloads ReadOnly Property File(name As String) As EmbeddedFile
    Get
      Dim foundFile As EmbeddedFile = Nothing

      foundFile = (From f As EmbeddedFile In Me.Files Where f.Name.ToLower() = name.ToLower()).FirstOrDefault()
      If (foundFile IsNot Nothing) Then
        Return foundFile
      End If

      foundFile = (From f As EmbeddedFile In Me.Files Where f.FullName.ToLower() = name.ToLower()).SingleOrDefault()
      Return foundFile
    End Get
  End Property

  <EditorBrowsable(EditorBrowsableState.Advanced)>
  Public Function Contains(name As String) As Boolean
    If ((From f As EmbeddedFile In Me.Files Where f.Name.ToLower() = name.ToLower()).FirstOrDefault() IsNot Nothing) Then
      Return True
    Else
      Return ((From f As EmbeddedFile In Me.Files Where f.FullName.ToLower() = name.ToLower()).FirstOrDefault() IsNot Nothing)
    End If
  End Function

#End Region

End Class

<DebuggerDisplay("EmbeddedFile ({Name})")>
Friend Class EmbeddedFile

#Region " Declarations & Constructor "

  <DebuggerBrowsable(DebuggerBrowsableState.Never)> Private _FullName As String
  <DebuggerBrowsable(DebuggerBrowsableState.Never)> Private _ContainingAssembly As Assembly
  <DebuggerBrowsable(DebuggerBrowsableState.Never)> Private _AssemblyDefaultNamespace As String

  Public Sub New(fullName As String, containingAssembly As Assembly, assemblyDefaultNamespace As String)

    _FullName = fullName
    _ContainingAssembly = containingAssembly
    _AssemblyDefaultNamespace = assemblyDefaultNamespace

    If (Not _AssemblyDefaultNamespace.EndsWith(".")) Then
      _AssemblyDefaultNamespace = _AssemblyDefaultNamespace & "."
    End If

  End Sub

#End Region

#Region " Properties "

  Public ReadOnly Property Name As String
    Get
      Return _FullName.Substring(_AssemblyDefaultNamespace.Length, _FullName.Length - _AssemblyDefaultNamespace.Length)
    End Get
  End Property

  <EditorBrowsable(EditorBrowsableState.Advanced)>
  Public ReadOnly Property FullName As String
    Get
      Return _FullName
    End Get
  End Property

  <EditorBrowsable(EditorBrowsableState.Advanced)>
  Public Overrides Function ToString() As String
    Return Me.FullName
  End Function

#End Region

#Region " Content Access "

  <EditorBrowsable(EditorBrowsableState.Advanced)>
  Public Function OpenStream() As Stream
    Return _ContainingAssembly.GetManifestResourceStream(_FullName)
  End Function

  Public Sub ExtractToFilesystem(targetPath As String)

    If (Path.GetFileName(targetPath) = String.Empty) Then
      targetPath = targetPath & Path.DirectorySeparatorChar & Me.Name
    End If

    Using fileStream = New FileStream(targetPath, FileMode.Create, FileAccess.Write)

      Using resStream = Me.OpenStream()
        resStream.CopyTo(fileStream)
      End Using

      fileStream.Close()
    End Using

  End Sub

  Public Function ReadText() As String
    Dim buffer As String

    Using resStream = Me.OpenStream()

      Using resStreamReader = New StreamReader(resStream, True)
        buffer = resStreamReader.ReadToEnd()
      End Using

      resStream.Close()
    End Using

    Return buffer
  End Function

  Public Function ReadImage() As Image
    Dim clonedInstance As Image

    Using resStream = Me.OpenStream()

      Using buffer = Image.FromStream(resStream)

        'if we dont do this, the image gets lost when closing the stream
        clonedInstance = New Bitmap(buffer.Width, buffer.Height)
        Using gfxForCloning = Graphics.FromImage(clonedInstance)
          gfxForCloning.DrawImage(buffer, New Point(0, 0))
        End Using

      End Using

      resStream.Close()
    End Using

    Return clonedInstance
  End Function

  Public Function ReadBinary() As Byte()
    Dim buffer As Byte()

    Using resStream = Me.OpenStream()
      ReDim buffer(CInt(resStream.Length - 1))
      resStream.Read(buffer, 0, CInt(resStream.Length))
      resStream.Close()
    End Using

    Return buffer
  End Function

#End Region

End Class

#Region " Publishing via Extension "

Friend Module ExtenstionsForReflectionAssembly

  <Extension(), EditorBrowsable(EditorBrowsableState.Always)>
  Public Function EmbeddedFiles(assembly As Assembly) As EmbeddedFileRepository
    Return New EmbeddedFileRepository(assembly)
  End Function

End Module

#End Region
