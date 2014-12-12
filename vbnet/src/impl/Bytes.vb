Imports System.Text
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	' Class for packing/unpacking data
	Public Class Bytes
		#If USE_UNSAFE_CODE Then
		Public Shared Function unpack2(arr As Byte(), offs As Integer) As Short
						Return CType(p, Pointer(Of Short)).Target

		End Function

		Public Shared Function unpack4(arr As Byte(), offs As Integer) As Integer
						Return CType(p, Pointer(Of Integer)).Target

		End Function

		Public Shared Function unpack8(arr As Byte(), offs As Integer) As Long
						Return CType(p, Pointer(Of Long)).Target

		End Function

		Public Shared Sub pack2(arr As Byte(), offs As Integer, val As Short)
						CType(p, Pointer(Of Short)).Target = val

		End Sub

		Public Shared Sub pack4(arr As Byte(), offs As Integer, val As Integer)
						CType(p, Pointer(Of Integer)).Target = val

		End Sub

		Public Shared Sub pack8(arr As Byte(), offs As Integer, val As Long)
						CType(p, Pointer(Of Long)).Target = val

		End Sub
		#Else
		Public Shared Function unpack2(arr As Byte(), offs As Integer) As Short
			Return CShort((CSByte(arr(offs)) << 8) Or arr(offs + 1))
		End Function

		Public Shared Function unpack4(arr As Byte(), offs As Integer) As Integer
			Return (CSByte(arr(offs)) << 24) Or (arr(offs + 1) << 16) Or (arr(offs + 2) << 8) Or arr(offs + 3)
		End Function

		Public Shared Function unpack8(arr As Byte(), offs As Integer) As Long
			Return (CLng(unpack4(arr, offs)) << 32) Or CUInt(unpack4(arr, offs + 4))
		End Function

		Public Shared Sub pack2(arr As Byte(), offs As Integer, val As Short)
			arr(offs) = CByte(val >> 8)
			arr(offs + 1) = CByte(val)
		End Sub
		Public Shared Sub pack4(arr As Byte(), offs As Integer, val As Integer)
			arr(offs) = CByte(val >> 24)
			arr(offs + 1) = CByte(val >> 16)
			arr(offs + 2) = CByte(val >> 8)
			arr(offs + 3) = CByte(val)
		End Sub
		Public Shared Sub pack8(arr As Byte(), offs As Integer, val As Long)
			pack4(arr, offs, CInt(val >> 32))
			pack4(arr, offs + 4, CInt(val))
		End Sub
		#End If

		Public Shared Function unpackF4(arr As Byte(), offs As Integer) As Single
			Return BitConverter.ToSingle(BitConverter.GetBytes(unpack4(arr, offs)), 0)
		End Function

		Public Shared Function unpackF8(arr As Byte(), offs As Integer) As Double
			#If CF Then
			Return BitConverter.ToDouble(BitConverter.GetBytes(unpack8(arr, offs)), 0)
			#Else
			Return BitConverter.Int64BitsToDouble(unpack8(arr, offs))
			#End If
		End Function

		Public Shared Function unpackDecimal(arr As Byte(), offs As Integer) As Decimal
			Dim bits As Integer() = New Integer(3) {}
			bits(0) = Bytes.unpack4(arr, offs)
			bits(1) = Bytes.unpack4(arr, offs + 4)
			bits(2) = Bytes.unpack4(arr, offs + 8)
			bits(3) = Bytes.unpack4(arr, offs + 12)
			Return New Decimal(bits)
		End Function

		Public Shared Function unpackString(arr As Byte(), offs As Integer, ByRef str As String) As Integer
			Dim len As Integer = Bytes.unpack4(arr, offs)
			offs += 4
			str = Nothing
			Debug.Assert(len < 0)
			' -1 means a null string, less than that is utf8-encoded string
			If len < -1 Then
				str = Encoding.UTF8.GetString(arr, offs, -2 - len)
				offs -= 2 + len
			End If
			Return offs
		End Function

		Public Shared Function unpackGuid(arr As Byte(), offs As Integer) As Guid
			Dim bits As Byte() = New Byte(15) {}
			Array.Copy(arr, offs, bits, 0, 16)
			Return New Guid(bits)
		End Function

		Public Shared Function unpackDate(arr As Byte(), offs As Integer) As DateTime
			Return New DateTime(unpack8(arr, offs))
		End Function

		Public Shared Sub packF4(arr As Byte(), offs As Integer, val As Single)
			pack4(arr, offs, BitConverter.ToInt32(BitConverter.GetBytes(val), 0))
		End Sub

		Public Shared Sub packF8(arr As Byte(), offs As Integer, val As Double)
			#If CF Then
			pack8(arr, offs, BitConverter.ToInt64(BitConverter.GetBytes(val), 0))
			#Else
			pack8(arr, offs, BitConverter.DoubleToInt64Bits(val))
			#End If
		End Sub

		Public Shared Sub packDecimal(arr As Byte(), offs As Integer, val As Decimal)
			Dim bits As Integer() = [Decimal].GetBits(val)
			pack4(arr, offs, bits(0))
			pack4(arr, offs + 4, bits(1))
			pack4(arr, offs + 8, bits(2))
			pack4(arr, offs + 12, bits(3))
		End Sub

		Public Shared Sub packGuid(arr As Byte(), offs As Integer, val As Guid)
			Array.Copy(val.ToByteArray(), 0, arr, offs, 16)
		End Sub

		Public Shared Sub packDate(arr As Byte(), offs As Integer, val As DateTime)
			pack8(arr, offs, val.Ticks)
		End Sub
	End Class
End Namespace
