Imports Volante.Impl
Namespace Volante

	Public Class Rc4File
		Inherits OsFile
		Public Overrides Sub Write(pos As Long, buf As Byte())
			If pos > length Then
				If zeroPage Is Nothing Then
					zeroPage = New Byte(Page.pageSize - 1) {}
					encrypt(zeroPage, 0, zeroPage, 0, Page.pageSize)
				End If
				Do
					MyBase.Write(length, zeroPage)
				Loop While (length += Page.pageSize) < pos
			End If

			If pos = length Then
				length += Page.pageSize
			End If

			encrypt(buf, 0, cipherBuf, 0, buf.Length)
			MyBase.Write(pos, cipherBuf)
		End Sub

		Public Overrides Function Read(pos As Long, buf As Byte()) As Integer
			If pos < length Then
				Dim rc As Integer = MyBase.Read(pos, buf)
				decrypt(buf, 0, buf, 0, rc)
				Return rc
			End If
			Return 0
		End Function

		Public Sub New(filePath As [String], key As [String])
			Me.New(filePath, key, False)
		End Sub

		Public Sub New(filePath As [String], key As [String], [readOnly] As Boolean)
			MyBase.New(filePath, [readOnly])
			length = file.Length And Not (Page.pageSize - 1)
			setKey(key)
		End Sub

		Private Sub setKey(key As [String])
			For counter As Integer = 0 To 255
				initState(counter) = CByte(counter)
			Next
			Dim index1 As Integer = 0
			Dim index2 As Integer = 0
			Dim length As Integer = key.Length
			For counter As Integer = 0 To 255
				index2 = (key(index1) + initState(counter) + index2) And &Hff
				Dim temp As Byte = initState(counter)
				initState(counter) = initState(index2)
				initState(index2) = temp
				index1 = (index1 + 1) Mod length
			Next
		End Sub

		Private Sub encrypt(clearText As Byte(), clearOff As Integer, cipherText As Byte(), cipherOff As Integer, len As Integer)
			x = InlineAssignHelper(y, 0)
			Array.Copy(initState, 0, state, 0, state.Length)
			For i As Integer = 0 To len - 1
				cipherText(cipherOff + i) = CByte(clearText(clearOff + i) Xor state(nextState()))
			Next
		End Sub

		Private Sub decrypt(cipherText As Byte(), cipherOff As Integer, clearText As Byte(), clearOff As Integer, len As Integer)
			x = InlineAssignHelper(y, 0)
			Array.Copy(initState, 0, state, 0, state.Length)
			For i As Integer = 0 To len - 1
				clearText(clearOff + i) = CByte(cipherText(cipherOff + i) Xor state(nextState()))
			Next
		End Sub

		Private Function nextState() As Integer
			x = (x + 1) And &Hff
			y = (y + state(x)) And &Hff
			Dim temp As Byte = state(x)
			state(x) = state(y)
			state(y) = temp
			Return (state(x) + state(y)) And &Hff
		End Function

		Private cipherBuf As Byte() = New Byte(Page.pageSize - 1) {}
		Private initState As Byte() = New Byte(255) {}
		Private state As Byte() = New Byte(255) {}
		Private x As Integer, y As Integer
		Private length As Long
		Private zeroPage As Byte()
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
	End Class
End Namespace
