
Namespace Volante

	''' <summary> Class for specifying key value (neededd to access object by key usig index)
	''' </summary>
	Public Class Key
        Public ReadOnly type As Volante.Impl.ClassDescriptor.FieldType
		Public ReadOnly ival As Integer
		Public ReadOnly lval As Long
		Public ReadOnly dval As Double
		Public ReadOnly oval As Object
		Public ReadOnly dec As Decimal
		Public ReadOnly guid As Guid
		Public ReadOnly inclusion As Integer

		''' <summary> Constructor of boolean key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Boolean)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of signed byte key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As SByte)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of byte key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Byte)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of char key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Char)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of short key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Short)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of unsigned short key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As UShort)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of int key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Integer)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of unsigned int key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As UInteger)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of enum key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As [Enum])
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of long key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Long)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of unsigned long key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As ULong)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of float key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Single)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of double key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Double)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of decimal key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Decimal)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of Guid key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Guid)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of date key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As DateTime)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of string key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As String)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of key of user defined type (boundary is inclusive)
		''' </summary>
		Public Sub New(v As IComparable)
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of array of char key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Char())
			Me.New(v, True)
		End Sub

		''' <summary> Constructor of array of byte key (boundary is inclusive)
		''' </summary>
		Public Sub New(v As Byte())
			Me.New(v, True)
		End Sub

		''' <summary>
		''' Constructor of compound key (boundary is inclusive)
		''' </summary>
		''' <param name="v">array of compound key values</param>
		Public Sub New(v As Object())
			Me.New(v, True)
		End Sub

		''' <summary>
		''' Constructor of compound key with two values (boundary is inclusive)
		''' </summary>
		''' <param name="v1">first value of compund key</param>
		''' <param name="v2">second value of compund key</param>
		Public Sub New(v1 As Object, v2 As Object)
			Me.New(New Object() {v1, v2}, True)
		End Sub

		''' <summary> Constructor of key with persistent object reference (boundary is inclusive)
		''' </summary>
		Public Sub New(v As IPersistent)
			Me.New(v, True)
		End Sub

        Friend Sub New(type As Volante.Impl.ClassDescriptor.FieldType, inclusive As Boolean)
            Me.type = type
            Me.inclusion = If(inclusive, 1, 0)
        End Sub

        Friend Sub New(type As Volante.Impl.ClassDescriptor.FieldType, oid As Integer)
            Me.type = type
            ival = oid
            Me.inclusion = 1
        End Sub

		''' <summary>Constructor of boolean key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Boolean, inclusive As Boolean)
            Me.New(Volante.Impl.ClassDescriptor.FieldType.tpBoolean, inclusive)
			ival = If(v, 1, 0)
		End Sub

		''' <summary>Constructor of signed byte key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As SByte, inclusive As Boolean)
            Me.New(Volante.Impl.ClassDescriptor.FieldType.tpSByte, inclusive)
			ival = v
		End Sub

		''' <summary>Constructor of byte key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Byte, inclusive As Boolean)
            Me.New(Volante.Impl.ClassDescriptor.FieldType.tpByte, inclusive)
			ival = v
		End Sub

        ''' <summary>Constructor of char key</summary>
		''' <param name="v">key value</param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Char, inclusive As Boolean)
            Me.New(Volante.Impl.ClassDescriptor.FieldType.tpChar, inclusive)
            ival = Microsoft.VisualBasic.AscW(v)
		End Sub

		''' <summary>Constructor of short key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Short, inclusive As Boolean)
            Me.New(Volante.Impl.ClassDescriptor.FieldType.tpShort, inclusive)
			ival = v
		End Sub

		''' <summary>Constructor of unsigned short key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As UShort, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpUShort, inclusive)
			ival = v
		End Sub

		''' <summary>Constructor of int key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As [Enum], inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpEnum, inclusive)
			ival = CInt(DirectCast(v, Object))
		End Sub

		''' <summary>Constructor of int key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Integer, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpInt, inclusive)
			ival = v
		End Sub

		''' <summary>Constructor of unsigned int key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As UInteger, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpUInt, inclusive)
			ival = CInt(v)
		End Sub

		''' <summary>Constructor of long key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Long, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpLong, inclusive)
			lval = v
		End Sub

		''' <summary>Constructor of unsigned long key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As ULong, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpULong, inclusive)
			lval = CLng(v)
		End Sub

		''' <summary>Constructor of float key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Single, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpFloat, inclusive)
			dval = v
		End Sub

		''' <summary>Constructor of double key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Double, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpDouble, inclusive)
			dval = v
		End Sub

		''' <summary>Constructor of decimal key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Decimal, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpDecimal, inclusive)
			dec = v
		End Sub

		''' <summary>Constructor of Guid key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Guid, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpGuid, inclusive)
			guid = v
		End Sub

		''' <summary>Constructor of date key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As DateTime, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpDate, inclusive)
			lval = v.Ticks
		End Sub

		''' <summary>Constructor of string key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As String, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpString, inclusive)
			oval = v
		End Sub

		''' <summary>Constructor of array of char key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As Char(), inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpString, inclusive)
			oval = v
		End Sub

		''' <summary>Constructor of array of byte key
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive</param>
		Public Sub New(v As Byte(), inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpArrayOfByte, inclusive)
			oval = v
		End Sub

		''' <summary>
		''' Constructor of compound key (boundary is inclusive)
		''' </summary>
		''' <param name="v">array of compound key values</param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive</param>
		Public Sub New(v As Object(), inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpArrayOfObject, inclusive)
			oval = v
		End Sub

		''' <summary>
		''' Constructor of compound key with two values (boundary is inclusive)
		''' </summary>
		''' <param name="v1">first value of compund key</param>
		''' <param name="v2">second value of compund key</param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive</param>
		Public Sub New(v1 As Object, v2 As Object, inclusive As Boolean)
			Me.New(New Object() {v1, v2}, inclusive)
		End Sub

		''' <summary>Constructor of key with persistent object reference
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As IPersistent, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpObject, inclusive)
			ival = If(v IsNot Nothing, v.Oid, 0)
			oval = v
		End Sub

		''' <summary>Constructor of key of user defined type
		''' </summary>
		''' <param name="v">key value
		''' </param>
		''' <param name="inclusive">whether boundary is inclusive or exclusive
		''' </param>
		Public Sub New(v As IComparable, inclusive As Boolean)
			Me.New(ClassDescriptor.FieldType.tpRaw, inclusive)
			oval = v
		End Sub
	End Class

	Class KeyBuilder
		Public Shared Function getKeyFromObject(o As Object) As Key
			If o Is Nothing Then
				Return Nothing
			ElseIf TypeOf o Is Byte Then
				Return New Key(CByte(o))
			ElseIf TypeOf o Is SByte Then
				Return New Key(CSByte(o))
			ElseIf TypeOf o Is Short Then
				Return New Key(CShort(o))
			ElseIf TypeOf o Is UShort Then
				Return New Key(CUShort(o))
			ElseIf TypeOf o Is Integer Then
				Return New Key(CInt(o))
			ElseIf TypeOf o Is UInteger Then
				Return New Key(CUInt(o))
			ElseIf TypeOf o Is Long Then
				Return New Key(CLng(o))
			ElseIf TypeOf o Is ULong Then
				Return New Key(CULng(o))
			ElseIf TypeOf o Is Single Then
				Return New Key(CSng(o))
			ElseIf TypeOf o Is Double Then
				Return New Key(CDbl(o))
			ElseIf TypeOf o Is Boolean Then
				Return New Key(CBool(o))
			ElseIf TypeOf o Is Char Then
				Return New Key(CChar(o))
			ElseIf TypeOf o Is [String] Then
				Return New Key(DirectCast(o, [String]))
			ElseIf TypeOf o Is DateTime Then
				Return New Key(CType(o, DateTime))
			ElseIf TypeOf o Is Byte() Then
				Return New Key(DirectCast(o, Byte()))
			ElseIf TypeOf o Is Object() Then
				Return New Key(DirectCast(o, Object()))
			ElseIf TypeOf o Is [Enum] Then
				Return New Key(DirectCast(o, [Enum]))
			ElseIf TypeOf o Is IPersistent Then
				Return New Key(DirectCast(o, IPersistent))
			ElseIf TypeOf o Is Guid Then
				Return New Key(CType(o, Guid))
			ElseIf TypeOf o Is [Decimal] Then
				Return New Key(CType(o, [Decimal]))
			ElseIf TypeOf o Is IComparable Then
				Return New Key(DirectCast(o, IComparable))
			End If
			Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_TYPE)
		End Function
	End Class
End Namespace
