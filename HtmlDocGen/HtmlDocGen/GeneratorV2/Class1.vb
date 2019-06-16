'Imports System
'Imports System.Collections.Generic
'Imports System.IO
'Imports HtmlDocGen

'<AttributeUsage(AttributeTargets.Class, AllowMultiple:=True, Inherited:=False)>
'Public Class TemplateCodeBehindAttribute
'  Inherits Attribute

'  Public Sub New(templateName As String)
'  End Sub

'End Class

'<AttributeUsage(AttributeTargets.Field Or AttributeTargets.Property)>
'Public Class InputArgumentAttribute
'  Inherits Attribute

'  Public Sub New(argumentName As String)
'  End Sub

'End Class

'<AttributeUsage(AttributeTargets.Field Or AttributeTargets.Property)>
'Public Class SubscibeDatasourceItemAttribute
'  Inherits Attribute

'  Public Sub New(itemName As String)
'  End Sub

'End Class

'<AttributeUsage(AttributeTargets.Field Or AttributeTargets.Property Or AttributeTargets.Method)>
'Public Class ProvidePlaceholderAttribute
'  Inherits Attribute

'  Public Sub New(itemName As String)
'  End Sub

'End Class

'Public Class DataSource

'  Public Shared ReadOnly Property Empty As DataSource = New DataSource

'  Public Shared Function FromDictionary(source As Dictionary(Of String, Object)) As DataSource

'  End Function

'  Public Shared Function FromDictionary(source As Dictionary(Of String, Func(Of Object))) As DataSource

'  End Function

'  Private Sub New()
'  End Sub

'  Public Sub New(items As Dictionary(Of String, Func(Of Object)))
'  End Sub

'  Public Sub New(items As Dictionary(Of String, Func(Of Object)), parentDatasource As DataSource)
'  End Sub

'End Class








'Public Class DefaultPlaceholderProcessingStrategy

'  Public Property TypeSpecificSerializer(targetType As Type) As IPlaceholderSerializer
'    Get

'    End Get
'    Set(value As IPlaceholderSerializer)

'    End Set
'  End Property

'  Public Property FallbackSerializer As IPlaceholderSerializer = New DefaultPlaceholderSerializer

'  Public Overridable Function SerializePlaceholderValueToString(placeholder As String, formatArg As String) As String

'  End Function

'  Public Property TypeSpecificBooleanEvaluator(targetType As Type) As IBooleanEvaluator
'    Get


'    End Get
'    Set(value As IBooleanEvaluator)

'    End Set
'  End Property

'  Public Property FallbackBooleanEvaluator As IBooleanEvaluator = New DefaultBooleanEvaluator

'  Public Overridable Function EvaluateBooleanPlaceholderValue(placeholder As String) As Boolean

'  End Function

'  Public Property TypeSpecificIterationProcessor(targetType As Type) As IIterationProcessor
'    Get

'    End Get
'    Set(value As IIterationProcessor)

'    End Set
'  End Property

'  Public Property FallbackIterationProcessor As IIterationProcessor = New DefaultIterationProcessor

'  Public Overridable Function IterateOverChildsOfPlaceholder(placeholder As String, iterationItemName As String) As String
'    'hier prüfen ob array oder ienumerable
'  End Function

'  Public Overridable Function RessolveAndOpenTemplate(templateName As String) As TextReader





'  End Function

'  Public Overridable Function RessolvePlaceholder(placeholder As String, dataSource As DataSource, Optional codeBehindInstance As Object = Nothing) As Object


'    'A) Resolve des objects!!!!

'    '1. magicvalue   this oder me  geht immer auf codebehing und knallt wenn dort keine prop da ist
'    '2. datasource (welche alle items der datasource + inputvars + (falls codebehind da ist)ProvidePlaceholderAttribtuierte Funktionen + properties

'    'B) per .notation navigierte member
'    '1. properties
'    '2. methods
'    '3. extensionmethods

'  End Function





'End Class

'Public Interface IBooleanEvaluator

'  Function EvaluateBoolean(value As Object) As Boolean

'End Interface

'Public Class DefaultBooleanEvaluator
'  Implements IBooleanEvaluator

'  Public Function EvaluateBoolean(value As Object) As Boolean Implements IBooleanEvaluator.EvaluateBoolean
'    Throw New NotImplementedException()
'  End Function
'End Class

'Public Interface IPlaceholderSerializer

'  ReadOnly Property DefaultSerializerArguments As String

'  Function Serilaize(value As Object, formatArg As String) As String

'End Interface

'Public Class DefaultPlaceholderSerializer
'  Implements IPlaceholderSerializer

'  Public ReadOnly Property DefaultSerializerArguments As String Implements IPlaceholderSerializer.DefaultSerializerArguments
'    Get
'      Throw New NotImplementedException()
'    End Get
'  End Property

'  Public Function Serilaize(value As Object, formatArg As String) As String Implements IPlaceholderSerializer.Serilaize
'    Throw New NotImplementedException()
'  End Function
'End Class

'Public Interface IIterationProcessor

'  Sub IterateOver(parent As Object, parentDataSource As DataSource, itemCallback As Action(Of Object, DataSource))

'End Interface

'Public Class DefaultIterationProcessor
'  Implements IIterationProcessor

'  Public Sub IterateOver(parent As Object, parentDataSource As DataSource, itemCallback As Action(Of Object, DataSource)) Implements IIterationProcessor.IterateOver




'  End Sub

'End Class