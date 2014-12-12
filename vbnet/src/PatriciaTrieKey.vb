#If WITH_PATRICIA Then
Imports System.Net
Imports System.Diagnostics

Namespace Volante
	''' Convert different type of keys to 64-bit long value used in Patricia trie 
	Public Class PatriciaTrieKey
		''' Bit mask representing bit vector.
		''' The last digit of the key is the right most bit of the mask
		Friend ReadOnly mask As ULong

		''' Length of bit vector (can not be larger than 64)
		Friend ReadOnly length As Integer

		Public Sub New(mask As ULong, length As Integer)
			Me.mask = mask
			Me.length = length
		End Sub

		Public Shared Function FromIpAddress(addr As IPAddress) As PatriciaTrieKey
			Dim bytes As Byte() = addr.GetAddressBytes()
			Dim mask As ULong = 0
			For i As Integer = 0 To bytes.Length - 1
				mask = (mask << 8) Or CUInt(bytes(i) And &Hff)
			Next
			Return New PatriciaTrieKey(mask, bytes.Length * 8)
		End Function

		Public Shared Function FromIpAddress(addr As String) As PatriciaTrieKey
			Dim mask As ULong = 0
			Dim pos As Integer = 0
			Dim len As Integer = 0
			Do
				Dim dot As Integer = addr.IndexOf("."C, pos)
				Dim part As [String] = If(dot < 0, addr.Substring(pos), addr.Substring(pos, dot - pos))
				pos = dot + 1
				Dim b As Integer = Int32.Parse(part)
				mask = (mask << 8) Or CUInt(b And &Hff)
				len += 8
			Loop While pos > 0
			Return New PatriciaTrieKey(mask, len)
		End Function

		Public Shared Function FromDecimalDigits(digits As String) As PatriciaTrieKey
			Dim mask As ULong = 0
			Dim n As Integer = digits.Length
			Debug.Assert(n <= 16)
			For i As Integer = 0 To n - 1
				Dim ch As Char = digits(i)
				Debug.Assert(ch >= "0"C AndAlso ch <= "9"C)
				mask = (mask << 4) Or CUInt(AscW(ch - "0"C))
			Next
			Return New PatriciaTrieKey(mask, n * 4)
		End Function

		Public Shared Function From7bitString(str As String) As PatriciaTrieKey
			Dim mask As ULong = 0
			Dim n As Integer = str.Length
			Debug.Assert(n * 7 <= 64)
			For i As Integer = 0 To n - 1
				Dim ch As Char = str(i)
				mask = (mask << 7) Or CUInt(ch And &H7f)
			Next
			Return New PatriciaTrieKey(mask, n * 7)
		End Function

		Public Shared Function From8bitString(str As String) As PatriciaTrieKey
			Dim mask As ULong = 0
			Dim n As Integer = str.Length
			Debug.Assert(n <= 8)
			For i As Integer = 0 To n - 1
				Dim ch As Char = str(i)
				mask = (mask << 8) Or CUInt(ch And &Hff)
			Next
			Return New PatriciaTrieKey(mask, n * 8)
		End Function

		Public Shared Function FromByteArray(arr As Byte()) As PatriciaTrieKey
			Dim mask As ULong = 0
			Dim n As Integer = arr.Length
			Debug.Assert(n <= 8)
			For i As Integer = 0 To n - 1
				mask = (mask << 8) Or CUInt(arr(i) And &Hff)
			Next
			Return New PatriciaTrieKey(mask, n * 8)
		End Function
	End Class
End Namespace
#End If
