#If WITH_PATRICIA Then
Imports System.Collections
Imports System.Collections.Generic
Imports Volante

Namespace Volante.Impl
	Class PTrie(Of T As {Class, IPersistent})
		Inherits PersistentCollection(Of T)
		Implements IPatriciaTrie(Of T)
		Private rootZero As PTrieNode
		Private rootOne As PTrieNode
		Private m_count As Integer

		Public Overrides Function GetEnumerator() As IEnumerator(Of T)
			Dim list As New List(Of T)()
			fill(list, rootZero)
			fill(list, rootOne)
			Return list.GetEnumerator()
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Private Shared Sub fill(list As List(Of T), node As PTrieNode)
			If node Is Nothing Then
				Return
			End If

			list.Add(node.obj)
			fill(list, node.childZero)
			fill(list, node.childOne)
		End Sub

		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return m_count
			End Get
		End Property

		Private Shared Function firstDigit(key As ULong, keyLength As Integer) As Integer
			Return CInt(key >> (keyLength - 1)) And 1
		End Function

		Private Shared Function getCommonPart(keyA As ULong, keyLengthA As Integer, keyB As ULong, keyLengthB As Integer) As Integer
			' truncate the keys so they are the same size (discard low bits)
			If keyLengthA > keyLengthB Then
				keyA >>= keyLengthA - keyLengthB
				keyLengthA = keyLengthB
			Else
				keyB >>= keyLengthB - keyLengthA
				keyLengthB = keyLengthA
			End If
			' now get common part
			Dim diff As ULong = keyA Xor keyB

			' finally produce common key part
			Dim count As Integer = 0
			While diff <> 0
				diff >>= 1
				count += 1
			End While
			Return keyLengthA - count
		End Function

		Public Function Add(key As PatriciaTrieKey, obj As T) As T
			Modify()
			m_count += 1

			If firstDigit(key.mask, key.length) = 1 Then
				If rootOne IsNot Nothing Then
					Return rootOne.add(key.mask, key.length, obj)
				Else
					rootOne = New PTrieNode(key.mask, key.length, obj)
					Return Nothing
				End If
			Else
				If rootZero IsNot Nothing Then
					Return rootZero.add(key.mask, key.length, obj)
				Else
					rootZero = New PTrieNode(key.mask, key.length, obj)
					Return Nothing
				End If
			End If
		End Function

		Public Function FindBestMatch(key As PatriciaTrieKey) As T
			If firstDigit(key.mask, key.length) = 1 Then
				If rootOne IsNot Nothing Then
					Return rootOne.findBestMatch(key.mask, key.length)
				End If
			Else
				If rootZero IsNot Nothing Then
					Return rootZero.findBestMatch(key.mask, key.length)
				End If
			End If
			Return Nothing
		End Function

		Public Function FindExactMatch(key As PatriciaTrieKey) As T
			If firstDigit(key.mask, key.length) = 1 Then
				If rootOne IsNot Nothing Then
					Return rootOne.findExactMatch(key.mask, key.length)
				End If
			Else
				If rootZero IsNot Nothing Then
					Return rootZero.findExactMatch(key.mask, key.length)
				End If
			End If
			Return Nothing
		End Function

		Public Function Remove(key As PatriciaTrieKey) As T
			Dim obj As T
			If firstDigit(key.mask, key.length) = 1 Then
				If rootOne IsNot Nothing Then
					obj = rootOne.remove(key.mask, key.length)
					If obj IsNot Nothing Then
						Modify()
						m_count -= 1
						If rootOne.isNotUsed() Then
							rootOne.Deallocate()
							rootOne = Nothing
						End If
						Return obj
					End If
				End If
			Else
				If rootZero IsNot Nothing Then
					obj = rootZero.remove(key.mask, key.length)
					If obj IsNot Nothing Then
						Modify()
						m_count -= 1
						If rootZero.isNotUsed() Then
							rootZero.Deallocate()
							rootZero = Nothing
						End If
						Return obj
					End If
				End If
			End If
			Return Nothing
		End Function

		Public Overrides Sub Clear()
			If rootOne IsNot Nothing Then
				rootOne.Deallocate()
				rootOne = Nothing
			End If
			If rootZero IsNot Nothing Then
				rootZero.Deallocate()
				rootZero = Nothing
			End If
			m_count = 0
		End Sub

		Private Class PTrieNode
			Inherits Persistent
			Friend key As ULong
			Friend keyLength As Integer
			Friend obj As T
			Friend childZero As PTrieNode
			Friend childOne As PTrieNode

			Friend Sub New(key As ULong, keyLength As Integer, obj As T)
				Me.obj = obj
				Me.key = key
				Me.keyLength = keyLength
			End Sub

			Private Sub New()
			End Sub

			Friend Function add(key As ULong, keyLength As Integer, obj As T) As T
				Dim prevObj As T
				If key = Me.key AndAlso keyLength = Me.keyLength Then
					Modify()
					' the new is matched exactly by this node's key, so just replace the node object
					prevObj = Me.obj
					Me.obj = obj
					Return prevObj
				End If
				Dim keyLengthCommon As Integer = getCommonPart(key, keyLength, Me.key, Me.keyLength)
				Dim keyLengthDiff As Integer = Me.keyLength - keyLengthCommon
				Dim keyCommon As ULong = key >> (keyLength - keyLengthCommon)
				Dim keyDiff As ULong = Me.key - (keyCommon << keyLengthDiff)
				' process diff with this node's key, if any
				If keyLengthDiff > 0 Then
					Modify()
					' create a new node with the diff
					Dim newNode As New PTrieNode(keyDiff, keyLengthDiff, Me.obj)
					' transfer infos of this node to the new node
					newNode.childZero = childZero
					newNode.childOne = childOne

					' update this node to hold common part
					Me.key = keyCommon
					Me.keyLength = keyLengthCommon
					Me.obj = Nothing

					' and set the new node as child of this node
					If firstDigit(keyDiff, keyLengthDiff) = 1 Then
						childZero = Nothing
						childOne = newNode
					Else
						childZero = newNode
						childOne = Nothing
					End If
				End If

				' process diff with the new key, if any
				If keyLength > keyLengthCommon Then
					' get diff with the new key
					keyLengthDiff = keyLength - keyLengthCommon
					keyDiff = key - (keyCommon << keyLengthDiff)

					' get which child we use as insertion point and do insertion (recursive)
					If firstDigit(keyDiff, keyLengthDiff) = 1 Then
						If childOne IsNot Nothing Then
							Return childOne.add(keyDiff, keyLengthDiff, obj)
						Else
							Modify()
							childOne = New PTrieNode(keyDiff, keyLengthDiff, obj)
							Return Nothing
						End If
					Else
						If childZero IsNot Nothing Then
							Return childZero.add(keyDiff, keyLengthDiff, obj)
						Else
							Modify()
							childZero = New PTrieNode(keyDiff, keyLengthDiff, obj)
							Return Nothing
						End If
					End If
				Else
					' the new key was containing within this node's original key, so just set this node as terminator
					prevObj = Me.obj
					Me.obj = obj
					Return prevObj
				End If
			End Function

			Friend Function findBestMatch(key As ULong, keyLength As Integer) As T
				If keyLength > Me.keyLength Then
					Dim keyLengthCommon As Integer = getCommonPart(key, keyLength, Me.key, Me.keyLength)
					Dim keyLengthDiff As Integer = keyLength - keyLengthCommon
					Dim keyCommon As ULong = key >> keyLengthDiff
					Dim keyDiff As ULong = key - (keyCommon << keyLengthDiff)

					If firstDigit(keyDiff, keyLengthDiff) = 1 Then
						If childOne IsNot Nothing Then
							Return childOne.findBestMatch(keyDiff, keyLengthDiff)
						End If
					Else
						If childZero IsNot Nothing Then
							Return childZero.findBestMatch(keyDiff, keyLengthDiff)
						End If
					End If
				End If
				Return obj
			End Function

			Friend Function findExactMatch(key As ULong, keyLength As Integer) As T
				If keyLength >= Me.keyLength Then
					If key = Me.key AndAlso keyLength = Me.keyLength Then
						Return obj
					Else
						Dim keyLengthCommon As Integer = getCommonPart(key, keyLength, Me.key, Me.keyLength)
						Dim keyLengthDiff As Integer = keyLength - keyLengthCommon
						Dim keyCommon As ULong = key >> keyLengthDiff
						Dim keyDiff As ULong = key - (keyCommon << keyLengthDiff)

						If firstDigit(keyDiff, keyLengthDiff) = 1 Then
							If childOne IsNot Nothing Then
								Return childOne.findBestMatch(keyDiff, keyLengthDiff)
							End If
						Else
							If childZero IsNot Nothing Then
								Return childZero.findBestMatch(keyDiff, keyLengthDiff)
							End If
						End If
					End If
				End If
				Return Nothing
			End Function

			Friend Function isNotUsed() As Boolean
				Return obj Is Nothing AndAlso childOne Is Nothing AndAlso childZero Is Nothing
			End Function

			Friend Function remove(key As ULong, keyLength As Integer) As T
				Dim obj As T
				If keyLength < Me.keyLength Then
					Return Nothing
				End If

				If key = Me.key AndAlso keyLength = Me.keyLength Then
					obj = Me.obj
					Me.obj = Nothing
					Return obj
				End If

				Dim keyLengthCommon As Integer = getCommonPart(key, keyLength, Me.key, Me.keyLength)
				Dim keyLengthDiff As Integer = keyLength - keyLengthCommon
				Dim keyCommon As ULong = key >> keyLengthDiff
				Dim keyDiff As ULong = key - (keyCommon << keyLengthDiff)

				If firstDigit(keyDiff, keyLengthDiff) = 1 Then
					If childOne Is Nothing Then
						Return Nothing
					End If

					obj = childOne.findBestMatch(keyDiff, keyLengthDiff)
					If obj Is Nothing Then
						Return Nothing
					End If

					If childOne.isNotUsed() Then
						Modify()
						childOne.Deallocate()
						childOne = Nothing
					End If
					Return obj
				End If

				If childZero Is Nothing Then
					Return Nothing
				End If

				obj = childZero.findBestMatch(keyDiff, keyLengthDiff)
				If obj Is Nothing Then
					Return Nothing
				End If

				If childZero.isNotUsed() Then
					Modify()
					childZero.Deallocate()
					childZero = Nothing
				End If
				Return obj
			End Function

			Public Overrides Sub Deallocate()
				If childOne IsNot Nothing Then
					childOne.Deallocate()
				End If

				If childZero IsNot Nothing Then
					childZero.Deallocate()
				End If

				MyBase.Deallocate()
			End Sub
		End Class
	End Class
End Namespace
#End If
