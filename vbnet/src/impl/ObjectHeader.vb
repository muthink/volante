Imports Volante
Namespace Volante.Impl

	Class ObjectHeader
		Friend Const Sizeof As Integer = 8

		Friend Shared Function getSize(arr As Byte(), offs As Integer) As Integer
			Return Bytes.unpack4(arr, offs)
		End Function
		Friend Shared Sub setSize(arr As Byte(), offs As Integer, size As Integer)
			Bytes.pack4(arr, offs, size)
		End Sub
		Friend Shared Overloads Function [getType](arr As Byte(), offs As Integer) As Integer
			Return Bytes.unpack4(arr, offs + 4)
		End Function
		Friend Shared Sub setType(arr As Byte(), offs As Integer, type As Integer)
			Bytes.pack4(arr, offs + 4, type)
		End Sub
	End Class
End Namespace
