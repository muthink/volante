Imports System.Text
Namespace Volante.Impl

	Public Class ByteBuffer
		Public Sub extend(size As Integer)
			If size > arr.Length Then
				Dim newLen As Integer = If(size > arr.Length * 2, size, arr.Length * 2)
				Dim newArr As Byte() = New Byte(newLen - 1) {}
				Array.Copy(arr, 0, newArr, 0, used)
				arr = newArr
			End If
			used = size
		End Sub

		Public Function toArray() As Byte()
			Dim result As Byte() = New Byte(used - 1) {}
			Array.Copy(arr, 0, result, 0, used)
			Return result
		End Function

		Public Function packI1(offs As Integer, val As Integer) As Integer
			extend(offs + 1)
			arr(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte(val)
			Return offs
		End Function

		Public Function packBool(offs As Integer, val As Boolean) As Integer
			extend(offs + 1)
			arr(System.Math.Max(System.Threading.Interlocked.Increment(offs),offs - 1)) = CByte(If(val, 1, 0))
			Return offs
		End Function

		Public Function packI2(offs As Integer, val As Integer) As Integer
			extend(offs + 2)
			Bytes.pack2(arr, offs, CShort(val))
			Return offs + 2
		End Function

		Public Function packI4(offs As Integer, val As Integer) As Integer
			extend(offs + 4)
			Bytes.pack4(arr, offs, val)
			Return offs + 4
		End Function

		Public Function packI8(offs As Integer, val As Long) As Integer
			extend(offs + 8)
			Bytes.pack8(arr, offs, val)
			Return offs + 8
		End Function

		Public Function packF4(offs As Integer, val As Single) As Integer
			extend(offs + 4)
			Bytes.packF4(arr, offs, val)
			Return offs + 4
		End Function

		Public Function packF8(offs As Integer, val As Double) As Integer
			extend(offs + 8)
			Bytes.packF8(arr, offs, val)
			Return offs + 8
		End Function

		Public Function packDecimal(offs As Integer, val As Decimal) As Integer
			extend(offs + 16)
			Bytes.packDecimal(arr, offs, val)
			Return offs + 16
		End Function

		Public Function packGuid(offs As Integer, val As Guid) As Integer
			extend(offs + 16)
			Bytes.packGuid(arr, offs, val)
			Return offs + 16
		End Function

		Public Function packDate(offs As Integer, val As DateTime) As Integer
			extend(offs + 8)
			Bytes.packDate(arr, offs, val)
			Return offs + 8
		End Function

		Public Function packString(offs As Integer, s As String) As Integer
			If s Is Nothing Then
				extend(offs + 4)
				Bytes.pack4(arr, offs, -1)
				offs += 4
				Return offs
			End If

			Dim bytes__1 As Byte() = Encoding.UTF8.GetBytes(s)
			extend(offs + 4 + bytes__1.Length)
			Bytes.pack4(arr, offs, -2 - bytes__1.Length)
			Array.Copy(bytes__1, 0, arr, offs + 4, bytes__1.Length)
			offs += 4 + bytes__1.Length
			Return offs
		End Function

		Public Sub New()
			arr = New Byte(63) {}
		End Sub

		Friend arr As Byte()
		Friend used As Integer
	End Class
End Namespace
