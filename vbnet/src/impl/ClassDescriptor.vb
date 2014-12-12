Imports System.Collections
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Diagnostics
Imports System.Text
Imports Volante
Namespace Volante.Impl

    <Serializable>
       Public NotInheritable Class ClassDescriptor
        Inherits Persistent
        Friend [next] As ClassDescriptor
        Friend name As [String]
        Friend allFields As FieldDescriptor()
        Friend hasReferences As Boolean
        Friend Shared lastModule As [Module]

        <Serializable>
        Public Class FieldDescriptor
            Inherits Persistent
            Friend fieldName As [String]
            Friend className As [String]
            Friend type As FieldType
            Friend valueDesc As ClassDescriptor
            <NonSerialized> _
            Friend field As FieldInfo
            <NonSerialized> _
            Friend Shadows recursiveLoading As Boolean
            <NonSerialized> _
            Friend constructor As MethodInfo

            Public Overloads Function equals(fd As FieldDescriptor) As Boolean
                Return fieldName.Equals(fd.fieldName) AndAlso className.Equals(fd.className) AndAlso valueDesc Is fd.valueDesc AndAlso type = fd.type
            End Function
        End Class
        <NonSerialized> _
        Friend cls As Type
        <NonSerialized> _
        Friend hasSubclasses As Boolean
        <NonSerialized> _
        Friend defaultConstructor As ConstructorInfo
        <NonSerialized> _
        Friend resolved As Boolean
        <NonSerialized> _
        Friend serializer As GeneratedSerializer

        Public Enum FieldType
            tpBoolean
            tpByte
            tpSByte
            tpShort
            tpUShort
            tpChar
            tpEnum
            tpInt
            tpUInt
            tpLong
            tpULong
            tpFloat
            tpDouble
            tpString
            tpDate
            tpObject
            tpOid
            tpValue
            tpRaw
            tpGuid
            tpDecimal
            tpLink
            tpArrayOfBoolean
            tpArrayOfByte
            tpArrayOfSByte
            tpArrayOfShort
            tpArrayOfUShort
            tpArrayOfChar
            tpArrayOfEnum
            tpArrayOfInt
            tpArrayOfUInt
            tpArrayOfLong
            tpArrayOfULong
            tpArrayOfFloat
            tpArrayOfDouble
            tpArrayOfString
            tpArrayOfDate
            tpArrayOfObject
            tpArrayOfOid
            tpArrayOfValue
            tpArrayOfRaw
            tpArrayOfGuid
            tpArrayOfDecimal
            tpLast
        End Enum

        ' tpBoolean,
        ' tpByte,
        ' tpSByte,
        ' tpShort, 
        ' tpUShort,
        ' tpChar,
        ' tpEnum,
        ' tpInt,
        ' tpUInt,
        ' tpLong,
        ' tpULong,
        ' tpFloat,
        ' tpDouble,
        ' tpString,
        ' tpDate,
        ' tpObject,
        ' tpOid,
        ' tpValue,
        ' tpRaw,
        ' tpGuid,
        ' tpDecimal,
        ' tpLink,
        ' tpArrayOfBoolean,
        ' tpArrayOfByte,
        ' tpArrayOfSByte,
        ' tpArrayOfShort, 
        ' tpArrayOfUShort,
        ' tpArrayOfChar,
        ' tpArrayOfEnum,
        ' tpArrayOfInt,
        ' tpArrayOfUInt,
        ' tpArrayOfLong,
        ' tpArrayOfULong,
        ' tpArrayOfFloat,
        ' tpArrayOfDouble,
        ' tpArrayOfString,
        ' tpArrayOfDate,
        ' tpArrayOfObject,
        ' tpArrayOfOid,
        ' tpArrayOfValue,
        ' tpArrayOfRaw,
        ' tpArrayOfGuid,
        ' tpArrayOfDecimal,
        Friend Shared Sizeof As Integer() = New Integer() {1, 1, 1, 2, 2, 2, _
            4, 4, 4, 8, 8, 4, _
            8, 0, 8, 4, 4, 0, _
            0, 16, 16, 0, 0, 0, _
            0, 0, 0, 0, 0, 0, _
            0, 0, 0, 0, 0, 0, _
            0, 0, 0, 0, 0, 0, _
            0}

        Friend Shared defaultConstructorProfile As Type() = New Type(-1) {}
        Friend Shared noArgs As Object() = New Object(-1) {}

#If CF Then
		Friend Shared Function parseEnum(type As Type, value As [String]) As Object
			For Each fi As FieldInfo In type.GetFields()
				If fi.IsLiteral AndAlso fi.Name.Equals(value) Then
					Return fi.GetValue(Nothing)
				End If
			Next
			Throw New ArgumentException(value)
		End Function
#End If

        Public Overloads Function equals(cd As ClassDescriptor) As Boolean
            If cd Is Nothing OrElse allFields.Length <> cd.allFields.Length Then
                Return False
            End If
            For i As Integer = 0 To allFields.Length - 1
                If Not allFields(i).equals(cd.allFields(i)) Then
                    Return False
                End If
            Next
            Return True
        End Function

        Friend Function newInstance() As [Object]
            Try
                Return defaultConstructor.Invoke(noArgs)
            Catch x As System.Exception
                Throw New DatabaseException(DatabaseException.ErrorCode.CONSTRUCTOR_FAILURE, cls, x)
            End Try
        End Function

#If CF Then
		Friend Sub generateSerializer()
		End Sub
#Else
        Private Shared serializerGenerator As CodeGenerator = CodeGenerator.Instance

        Friend Sub generateSerializer()
            If Not cls.IsPublic OrElse defaultConstructor Is Nothing OrElse Not defaultConstructor.IsPublic Then
                Return
            End If

            Dim flds As FieldDescriptor() = allFields
            Dim i As Integer = 0, n As Integer = flds.Length
            While i < n
                Dim fd As FieldDescriptor = flds(i)
                Select Case fd.type
                    Case FieldType.tpValue, FieldType.tpArrayOfValue, FieldType.tpArrayOfObject, FieldType.tpArrayOfEnum, FieldType.tpArrayOfRaw, FieldType.tpLink, _
                        FieldType.tpArrayOfOid
                        Return
                    Case Else
                        Exit Select
                End Select
                Dim f As FieldInfo = flds(i).field
                If f Is Nothing OrElse Not f.IsPublic Then
                    Return
                End If
                i += 1
            End While
            serializer = serializerGenerator.Generate(Me)
        End Sub

        Private Shared Function isObjectProperty(cls As Type, f As FieldInfo) As Boolean
            Return GetType(PersistentWrapper).IsAssignableFrom(cls) AndAlso f.Name.StartsWith("r_")
        End Function
#End If

        Private Function GetConstructor(f As FieldInfo, name As String) As MethodInfo
            Dim mi As MethodInfo = GetType(DatabaseImpl).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)
            'return mi.BindGenericParameters(f.FieldType.GetGenericArguments());
            'TODO: verify it's MakeGenericMethod
            Return mi.MakeGenericMethod(f.FieldType.GetGenericArguments())
        End Function

        Friend Shared Function getTypeName(t As Type) As [String]
            If t.IsGenericType Then
                Dim genericArgs As Type() = t.GetGenericArguments()
                t = t.GetGenericTypeDefinition()
                Dim buf As New StringBuilder(t.FullName)
                buf.Append("="c)
                Dim sep As Char = "["c
                For j As Integer = 0 To genericArgs.Length - 1
                    buf.Append(sep)
                    sep = ","c
                    buf.Append(getTypeName(genericArgs(j)))
                Next
                buf.Append("]"c)
                Return buf.ToString()
            End If
            Return t.FullName
        End Function

        Private Shared Function isVolanteInternalType(t As Type) As Boolean
            Return t.[Namespace] = GetType(IPersistent).[Namespace] AndAlso t IsNot GetType(IPersistent) AndAlso t IsNot GetType(PersistentContext) AndAlso t IsNot GetType(Persistent)
        End Function

        Friend Sub buildFieldList(db As DatabaseImpl, cls As System.Type, list As ArrayList)
            Dim superclass As System.Type = cls.BaseType
            If superclass IsNot Nothing AndAlso superclass IsNot GetType(MarshalByRefObject) Then
                buildFieldList(db, superclass, list)
            End If
            Dim flds As System.Reflection.FieldInfo() = cls.GetFields(BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public] Or BindingFlags.DeclaredOnly)
#If Not CF Then
            Dim isWrapper As Boolean = GetType(PersistentWrapper).IsAssignableFrom(cls)
#End If
            Dim hasTransparentAttribute As Boolean = cls.GetCustomAttributes(GetType(TransparentPersistenceAttribute), True).Length <> 0

            For i As Integer = 0 To flds.Length - 1
                Dim f As FieldInfo = flds(i)
                If Not f.IsNotSerialized AndAlso Not f.IsStatic Then
                    Dim fd As New FieldDescriptor()
                    fd.field = f
                    fd.fieldName = f.Name
                    fd.className = getTypeName(cls)
                    Dim fieldType__1 As Type = f.FieldType
                    Dim type As FieldType = getTypeCode(fieldType__1)
                    Select Case type
#If Not CF Then
                        Case FieldType.tpInt
                            If isWrapper AndAlso isObjectProperty(cls, f) Then
                                hasReferences = True
                                type = FieldType.tpOid
                            End If
                            Exit Select
#End If
                        Case FieldType.tpArrayOfOid
                            fd.constructor = GetConstructor(f, "ConstructArray")
                            hasReferences = True
                            Exit Select
                        Case FieldType.tpLink
                            fd.constructor = GetConstructor(f, "ConstructLink")
                            hasReferences = True
                            Exit Select

                        Case FieldType.tpArrayOfObject, FieldType.tpObject
                            hasReferences = True
                            If hasTransparentAttribute AndAlso isVolanteInternalType(fieldType__1) Then
                                fd.recursiveLoading = True
                            End If
                            Exit Select
                        Case FieldType.tpValue
                            fd.valueDesc = db.getClassDescriptor(f.FieldType)
                            hasReferences = hasReferences Or fd.valueDesc.hasReferences
                            Exit Select
                        Case FieldType.tpArrayOfValue
                            fd.valueDesc = db.getClassDescriptor(f.FieldType.GetElementType())
                            hasReferences = hasReferences Or fd.valueDesc.hasReferences
                            Exit Select
                    End Select
                    fd.type = type
                    list.Add(fd)
                End If
            Next
        End Sub

        Public Shared Function getTypeCode(c As System.Type) As FieldType
            Dim type As FieldType
            If c.Equals(GetType(Byte)) Then
                type = FieldType.tpByte
            ElseIf c.Equals(GetType(SByte)) Then
                type = FieldType.tpSByte
            ElseIf c.Equals(GetType(Short)) Then
                type = FieldType.tpShort
            ElseIf c.Equals(GetType(UShort)) Then
                type = FieldType.tpUShort
            ElseIf c.Equals(GetType(Char)) Then
                type = FieldType.tpChar
            ElseIf c.Equals(GetType(Integer)) Then
                type = FieldType.tpInt
            ElseIf c.Equals(GetType(UInteger)) Then
                type = FieldType.tpUInt
            ElseIf c.Equals(GetType(Long)) Then
                type = FieldType.tpLong
            ElseIf c.Equals(GetType(ULong)) Then
                type = FieldType.tpULong
            ElseIf c.Equals(GetType(Single)) Then
                type = FieldType.tpFloat
            ElseIf c.Equals(GetType(Double)) Then
                type = FieldType.tpDouble
            ElseIf c.Equals(GetType(System.String)) Then
                type = FieldType.tpString
            ElseIf c.Equals(GetType(Boolean)) Then
                type = FieldType.tpBoolean
            ElseIf c.Equals(GetType(System.DateTime)) Then
                type = FieldType.tpDate
            ElseIf c.IsEnum Then
                type = FieldType.tpEnum
            ElseIf c.Equals(GetType(Decimal)) Then
                type = FieldType.tpDecimal
            ElseIf c.Equals(GetType(Guid)) Then
                type = FieldType.tpGuid
            ElseIf GetType(IPersistent).IsAssignableFrom(c) Then
                type = FieldType.tpObject
            ElseIf GetType(ValueType).IsAssignableFrom(c) Then
                type = FieldType.tpValue
            ElseIf GetType(IGenericPArray).IsAssignableFrom(c) Then
                type = FieldType.tpArrayOfOid
            ElseIf GetType(IGenericLink).IsAssignableFrom(c) Then
                type = FieldType.tpLink
            ElseIf c.IsArray Then
                type = getTypeCode(c.GetElementType())
                If CInt(type) >= CInt(FieldType.tpLink) Then
                    Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_TYPE, c)
                End If
                type = CType(CInt(type) + CInt(FieldType.tpArrayOfBoolean), FieldType)
            Else
                type = FieldType.tpRaw
            End If
            Return type
        End Function

        Friend Sub New()
        End Sub

        Friend Sub New(db As DatabaseImpl, cls As Type)
            Me.cls = cls
            name = getTypeName(cls)
            Dim list As New ArrayList()
            buildFieldList(db, cls, list)
            allFields = DirectCast(list.ToArray(GetType(FieldDescriptor)), FieldDescriptor())
            defaultConstructor = cls.GetConstructor(BindingFlags.Instance Or BindingFlags.Instance Or BindingFlags.[Public] Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly, Nothing, defaultConstructorProfile, Nothing)
            If defaultConstructor Is Nothing AndAlso Not GetType(ValueType).IsAssignableFrom(cls) Then
                Throw New DatabaseException(DatabaseException.ErrorCode.DESCRIPTOR_FAILURE, cls)
            End If
            resolved = True
        End Sub

        Friend Shared Function lookup(db As IDatabase, name As [String]) As Type
            Dim resolvedTypes = DirectCast(db, DatabaseImpl).resolvedTypes
            SyncLock resolvedTypes
                Dim cls As Type = Nothing
                Dim ok = resolvedTypes.TryGetValue(name, cls)
                If ok Then
                    Return cls
                End If
                Dim loader As IClassLoader = db.Loader
                If loader IsNot Nothing Then
                    cls = loader.LoadClass(name)
                    If cls IsNot Nothing Then
                        resolvedTypes(name) = cls
                        Return cls
                    End If
                End If
                Dim last As [Module] = lastModule
                If last IsNot Nothing Then
                    cls = last.[GetType](name)
                    If cls IsNot Nothing Then
                        resolvedTypes(name) = cls
                        Return cls
                    End If
                End If

                Dim p As Integer = name.IndexOf("="c)
                If p >= 0 Then
                    Dim genericType As Type = lookup(db, name.Substring(0, p))
                    Dim genericParams As Type() = New Type(genericType.GetGenericArguments().Length - 1) {}
                    Dim nest As Integer = 0
                    p += 2
                    Dim i As Integer = p
                    Dim n As Integer = 0

                    While True
                        Select Case name(System.Math.Max(System.Threading.Interlocked.Increment(i), i - 1))
                            Case "["c
                                nest += 1
                                Exit Select
                            Case "]"c
                                If System.Threading.Interlocked.Decrement(nest) < 0 Then
                                    genericParams(System.Math.Max(System.Threading.Interlocked.Increment(n), n - 1)) = lookup(db, name.Substring(p, i - p - 1))
                                    Debug.Assert(n = genericParams.Length)
                                    cls = genericType.MakeGenericType(genericParams)
                                    If cls Is Nothing Then
                                        Throw New DatabaseException(DatabaseException.ErrorCode.CLASS_NOT_FOUND, name)
                                    End If
                                    resolvedTypes(name) = cls
                                    Return cls
                                End If
                                Exit Select
                            Case ","c
                                If nest = 0 Then
                                    genericParams(System.Math.Max(System.Threading.Interlocked.Increment(n), n - 1)) = lookup(db, name.Substring(p, i - p - 1))
                                    p = i
                                End If
                                Exit Select
                        End Select
                    End While
                End If

#If CF Then
				For Each ass As Assembly In DatabaseImpl.assemblies
#Else
                For Each ass As Assembly In AppDomain.CurrentDomain.GetAssemblies()
#End If
                    For Each [mod] As [Module] In ass.GetModules()
                        Dim t As Type = [mod].[GetType](name)
                        If t IsNot Nothing Then
                            If cls IsNot Nothing Then
                                Throw New DatabaseException(DatabaseException.ErrorCode.AMBIGUITY_CLASS, name)
                            Else
                                lastModule = [mod]
                                cls = t
                            End If
                        End If
                    Next
                Next
#If Not CF Then
                If cls Is Nothing AndAlso name.EndsWith("Wrapper") Then
                    Dim originalType As Type = lookup(db, name.Substring(0, name.Length - 7))
                    SyncLock db
                        cls = DirectCast(db, DatabaseImpl).getWrapper(originalType)
                    End SyncLock
                End If
#End If
                If cls Is Nothing Then
                    Throw New DatabaseException(DatabaseException.ErrorCode.CLASS_NOT_FOUND, name)
                End If
                resolvedTypes(name) = cls
                Return cls
            End SyncLock
        End Function

        Public Overrides Sub OnLoad()
            cls = lookup(Database, name)
            Dim n As Integer = allFields.Length
            Dim hasTransparentAttribute As Boolean = cls.GetCustomAttributes(GetType(TransparentPersistenceAttribute), True).Length <> 0
            Dim i As Integer = n
            While System.Threading.Interlocked.Decrement(i) >= 0
                Dim fd As FieldDescriptor = allFields(i)
                fd.Load()
                fd.field = cls.GetField(fd.fieldName, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public])
                If hasTransparentAttribute AndAlso fd.type = FieldType.tpObject AndAlso isVolanteInternalType(fd.field.FieldType) Then
                    fd.recursiveLoading = True
                End If

                Select Case fd.type
                    Case FieldType.tpArrayOfOid
                        fd.constructor = GetConstructor(fd.field, "ConstructArray")
                        Exit Select
                    Case FieldType.tpLink
                        fd.constructor = GetConstructor(fd.field, "ConstructLink")
                        Exit Select
                    Case Else
                        Exit Select
                End Select
            End While

            defaultConstructor = cls.GetConstructor(BindingFlags.Instance Or BindingFlags.Instance Or BindingFlags.[Public] Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly, Nothing, defaultConstructorProfile, Nothing)
            If defaultConstructor Is Nothing AndAlso Not GetType(ValueType).IsAssignableFrom(cls) Then
                Throw New DatabaseException(DatabaseException.ErrorCode.DESCRIPTOR_FAILURE, cls)
            End If
            Dim s As DatabaseImpl = DirectCast(Database, DatabaseImpl)
            If Not s.classDescMap.ContainsKey(cls) Then
                DirectCast(Database, DatabaseImpl).classDescMap.Add(cls, Me)
            End If
        End Sub

        Friend Sub resolve()
            If resolved Then
                Return
            End If

            Dim classStorage As DatabaseImpl = DirectCast(Database, DatabaseImpl)
            Dim desc As New ClassDescriptor(classStorage, cls)
            resolved = True
            If Not desc.equals(Me) Then
                classStorage.registerClassDescriptor(desc)
            End If
        End Sub

        Public Overrides Function RecursiveLoading() As Boolean
            Return False
        End Function
    End Class
End Namespace
