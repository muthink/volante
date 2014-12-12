#If WITH_OLD_BTREE Then
Imports System.Collections
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Friend Class OldBtreePage
		Friend Const firstKeyOffs As Integer = 4
		Friend Const keySpace As Integer = Page.pageSize - firstKeyOffs
		Friend Const strKeySize As Integer = 8
		Friend Const maxItems As Integer = keySpace / 4

		Friend Shared Function getnItems(pg As Page) As Integer
			Return Bytes.unpack2(pg.data, 0)
		End Function
		Friend Shared Function getSize(pg As Page) As Integer
			Return Bytes.unpack2(pg.data, 2)
		End Function
		Friend Shared Function getKeyStrOid(pg As Page, index As Integer) As Integer
			Return Bytes.unpack4(pg.data, firstKeyOffs + index * 8)
		End Function
		Friend Shared Function getKeyStrSize(pg As Page, index As Integer) As Integer
			Return Bytes.unpack2(pg.data, firstKeyOffs + index * 8 + 4)
		End Function
		Friend Shared Function getKeyStrOffs(pg As Page, index As Integer) As Integer
			Return Bytes.unpack2(pg.data, firstKeyOffs + index * 8 + 6)
		End Function

		Friend Shared Function getReference(pg As Page, index As Integer) As Integer
			Return Bytes.unpack4(pg.data, firstKeyOffs + index * 4)
		End Function

		Friend Shared Sub setnItems(pg As Page, nItems As Integer)
			Bytes.pack2(pg.data, 0, CShort(nItems))
		End Sub
		Friend Shared Sub setSize(pg As Page, size As Integer)
			Bytes.pack2(pg.data, 2, CShort(size))
		End Sub
		Friend Shared Sub setKeyStrOid(pg As Page, index As Integer, oid As Integer)
			Bytes.pack4(pg.data, firstKeyOffs + index * 8, oid)
		End Sub
		Friend Shared Sub setKeyStrSize(pg As Page, index As Integer, size As Integer)
			Bytes.pack2(pg.data, firstKeyOffs + index * 8 + 4, CShort(size))
		End Sub
		Friend Shared Sub setKeyStrOffs(pg As Page, index As Integer, offs As Integer)
			Bytes.pack2(pg.data, firstKeyOffs + index * 8 + 6, CShort(offs))
		End Sub
		Friend Shared Sub setKeyStrChars(pg As Page, offs As Integer, str As Char())
			Dim len As Integer = str.Length
			For i As Integer = 0 To len - 1
				Bytes.pack2(pg.data, firstKeyOffs + offs, CShort(AscW(str(i))))
				offs += 2
			Next
		End Sub
		Friend Shared Sub setKeyBytes(pg As Page, offs As Integer, bytes As Byte())
			Array.Copy(bytes, 0, pg.data, firstKeyOffs + offs, bytes.Length)
		End Sub

		Friend Shared Sub setReference(pg As Page, index As Integer, oid As Integer)
			Bytes.pack4(pg.data, firstKeyOffs + index * 4, oid)
		End Sub

		Friend Shared Function compare(key As Key, pg As Page, i As Integer) As Integer
			Dim i8 As Long
			Dim u8 As ULong
			Dim i4 As Integer
			Dim u4 As UInteger
			Dim r4 As Single
			Dim r8 As Double
			Select Case key.type
				Case ClassDescriptor.FieldType.tpSByte
					Return CSByte(key.ival) - CSByte(pg.data(OldBtreePage.firstKeyOffs + i))

				Case ClassDescriptor.FieldType.tpBoolean, ClassDescriptor.FieldType.tpByte
					Return CByte(key.ival) - pg.data(OldBtreePage.firstKeyOffs + i)

				Case ClassDescriptor.FieldType.tpShort
					Return CShort(key.ival) - Bytes.unpack2(pg.data, OldBtreePage.firstKeyOffs + i * 2)
				Case ClassDescriptor.FieldType.tpUShort
					Return CUShort(key.ival) - CUShort(Bytes.unpack2(pg.data, OldBtreePage.firstKeyOffs + i * 2))

				Case ClassDescriptor.FieldType.tpChar
					Return CChar(key.ival) - CChar(Bytes.unpack2(pg.data, OldBtreePage.firstKeyOffs + i * 2))

				Case ClassDescriptor.FieldType.tpObject, ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpOid, ClassDescriptor.FieldType.tpEnum
					u4 = CUInt(Bytes.unpack4(pg.data, OldBtreePage.firstKeyOffs + i * 4))
					Return If(CUInt(key.ival) < u4, -1, If(CUInt(key.ival) = u4, 0, 1))

				Case ClassDescriptor.FieldType.tpInt
					i4 = Bytes.unpack4(pg.data, OldBtreePage.firstKeyOffs + i * 4)
					Return If(key.ival < i4, -1, If(key.ival = i4, 0, 1))

				Case ClassDescriptor.FieldType.tpLong
					i8 = Bytes.unpack8(pg.data, OldBtreePage.firstKeyOffs + i * 8)
					Return If(key.lval < i8, -1, If(key.lval = i8, 0, 1))

				Case ClassDescriptor.FieldType.tpDate, ClassDescriptor.FieldType.tpULong
					u8 = CULng(Bytes.unpack8(pg.data, OldBtreePage.firstKeyOffs + i * 8))
					Return If(CULng(key.lval) < u8, -1, If(CULng(key.lval) = u8, 0, 1))

				Case ClassDescriptor.FieldType.tpFloat
					r4 = Bytes.unpackF4(pg.data, OldBtreePage.firstKeyOffs + i * 4)
					Return If(key.dval < r4, -1, If(key.dval = r4, 0, 1))

				Case ClassDescriptor.FieldType.tpDouble
					r8 = Bytes.unpackF8(pg.data, OldBtreePage.firstKeyOffs + i * 8)
					Return If(key.dval < r8, -1, If(key.dval = r8, 0, 1))

				Case ClassDescriptor.FieldType.tpDecimal
					Return key.dec.CompareTo(Bytes.unpackDecimal(pg.data, OldBtreePage.firstKeyOffs + i * 16))

				Case ClassDescriptor.FieldType.tpGuid
					Return key.guid.CompareTo(Bytes.unpackGuid(pg.data, OldBtreePage.firstKeyOffs + i * 16))
			End Select
			Debug.Assert(False, "Invalid type")
			Return 0
		End Function

		Friend Shared Function compareStr(key As Key, pg As Page, i As Integer) As Integer
			Dim chars As Char() = Nothing
			Dim s As String = TryCast(key.oval, String)
			If s IsNot Nothing Then
				chars = s.ToCharArray()
			Else
				chars = DirectCast(key.oval, Char())
			End If
			Dim alen As Integer = chars.Length
			Dim blen As Integer = OldBtreePage.getKeyStrSize(pg, i)
			Dim minlen As Integer = If(alen < blen, alen, blen)
			Dim offs As Integer = OldBtreePage.getKeyStrOffs(pg, i) + OldBtreePage.firstKeyOffs
			Dim b As Byte() = pg.data
			For j As Integer = 0 To minlen - 1
				Dim diff As Integer = chars(j) - CChar(Bytes.unpack2(b, offs))
				If diff <> 0 Then
					Return diff
				End If
				offs += 2
			Next
			Return alen - blen
		End Function

		Friend Shared Function find(db As DatabaseImpl, pageId As Integer, firstKey As Key, lastKey As Key, tree As OldBtree, height As Integer, _
			result As ArrayList) As Boolean
			Dim pg As Page = db.getPage(pageId)
			Dim l As Integer = 0, n As Integer = getnItems(pg), r As Integer = n
			Dim oid As Integer
			height -= 1
			Try
				If tree.FieldType = ClassDescriptor.FieldType.tpString Then
					If firstKey IsNot Nothing Then
						While l < r
							Dim i As Integer = (l + r) >> 1
							If compareStr(firstKey, pg, i) >= firstKey.inclusion Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Debug.Assert(r = l)
					End If
					If lastKey IsNot Nothing Then
						If height = 0 Then
							While l < n
								If -compareStr(lastKey, pg, l) >= lastKey.inclusion Then
									Return False
								End If
								oid = getKeyStrOid(pg, l)
								result.Add(db.lookupObject(oid, Nothing))
								l += 1
							End While
						Else
							Do
								If Not find(db, getKeyStrOid(pg, l), firstKey, lastKey, tree, height, _
									result) Then
									Return False
								End If
								If l = n Then
									Return True
								End If
							Loop While compareStr(lastKey, pg, System.Math.Max(System.Threading.Interlocked.Increment(l),l - 1)) >= 0
							Return False
						End If
					Else
						If height = 0 Then
							While l < n
								oid = getKeyStrOid(pg, l)
								result.Add(db.lookupObject(oid, Nothing))
								l += 1
							End While
						Else
							Do
								If Not find(db, getKeyStrOid(pg, l), firstKey, lastKey, tree, height, _
									result) Then
									Return False
								End If
							Loop While System.Threading.Interlocked.Increment(l) <= n
						End If
					End If
				ElseIf tree.FieldType = ClassDescriptor.FieldType.tpArrayOfByte Then
					If firstKey IsNot Nothing Then
						While l < r
							Dim i As Integer = (l + r) >> 1
							If tree.compareByteArrays(firstKey, pg, i) >= firstKey.inclusion Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Debug.Assert(r = l)
					End If
					If lastKey IsNot Nothing Then
						If height = 0 Then
							While l < n
								If -tree.compareByteArrays(lastKey, pg, l) >= lastKey.inclusion Then
									Return False
								End If
								oid = getKeyStrOid(pg, l)
								result.Add(db.lookupObject(oid, Nothing))
								l += 1
							End While
						Else
							Do
								If Not find(db, getKeyStrOid(pg, l), firstKey, lastKey, tree, height, _
									result) Then
									Return False
								End If
								If l = n Then
									Return True
								End If
							Loop While tree.compareByteArrays(lastKey, pg, System.Math.Max(System.Threading.Interlocked.Increment(l),l - 1)) >= 0
							Return False
						End If
					Else
						If height = 0 Then
							While l < n
								oid = getKeyStrOid(pg, l)
								result.Add(db.lookupObject(oid, Nothing))
								l += 1
							End While
						Else
							Do
								If Not find(db, getKeyStrOid(pg, l), firstKey, lastKey, tree, height, _
									result) Then
									Return False
								End If
							Loop While System.Threading.Interlocked.Increment(l) <= n
						End If
					End If
				Else
					If firstKey IsNot Nothing Then
						While l < r
							Dim i As Integer = (l + r) >> 1
							If compare(firstKey, pg, i) >= firstKey.inclusion Then
								l = i + 1
							Else
								r = i
							End If
						End While
						Debug.Assert(r = l)
					End If
					If lastKey IsNot Nothing Then
						If height = 0 Then
							While l < n
								If -compare(lastKey, pg, l) >= lastKey.inclusion Then
									Return False
								End If
								oid = getReference(pg, maxItems - 1 - l)
								result.Add(db.lookupObject(oid, Nothing))
								l += 1
							End While
							Return True
						Else
							Do
								If Not find(db, getReference(pg, maxItems - 1 - l), firstKey, lastKey, tree, height, _
									result) Then
									Return False
								End If

								If l = n Then
									Return True
								End If
							Loop While compare(lastKey, pg, System.Math.Max(System.Threading.Interlocked.Increment(l),l - 1)) >= 0
							Return False
						End If
					End If
					If height = 0 Then
						While l < n
							oid = getReference(pg, maxItems - 1 - l)
							result.Add(db.lookupObject(oid, Nothing))
							l += 1
						End While
					Else
						Do
							If Not find(db, getReference(pg, maxItems - 1 - l), firstKey, lastKey, tree, height, _
								result) Then
								Return False
							End If
						Loop While System.Threading.Interlocked.Increment(l) <= n
					End If
				End If
			Finally
				db.pool.unfix(pg)
			End Try
			Return True
		End Function

		Private Shared Function comparePrefix(key As String, pg As Page, i As Integer) As Integer
			Dim alen As Integer = key.Length
			Dim blen As Integer = OldBtreePage.getKeyStrSize(pg, i)
			Dim minlen As Integer = If(alen < blen, alen, blen)
			Dim offs As Integer = OldBtreePage.getKeyStrOffs(pg, i) + OldBtreePage.firstKeyOffs
			Dim b As Byte() = pg.data
			For j As Integer = 0 To minlen - 1
				Dim c As Char = CChar(Bytes.unpack2(b, offs))
				Dim diff As Integer = key(j) - c
				If diff <> 0 Then
					Return diff
				End If
				offs += 2
			Next
			Return minlen - alen
		End Function

		Friend Shared Function prefixSearch(db As DatabaseImpl, pageId As Integer, key As String, height As Integer, result As ArrayList) As Boolean
			Dim pg As Page = db.getPage(pageId)
			Dim l As Integer = 0, n As Integer = getnItems(pg), r As Integer = n
			Dim oid As Integer
			height -= 1
			Try
				While l < r
					Dim i As Integer = (l + r) >> 1
					If comparePrefix(key, pg, i) > 0 Then
						l = i + 1
					Else
						r = i
					End If
				End While
				Debug.Assert(r = l)
				If height = 0 Then
					While l < n
						If comparePrefix(key, pg, l) < 0 Then
							Return False
						End If

						oid = getKeyStrOid(pg, l)
						result.Add(db.lookupObject(oid, Nothing))
						l += 1
					End While
				Else
					Do
						If Not prefixSearch(db, getKeyStrOid(pg, l), key, height, result) Then
							Return False
						End If
						If l = n Then
							Return True
						End If
					Loop While comparePrefix(key, pg, System.Math.Max(System.Threading.Interlocked.Increment(l),l - 1)) >= 0
					Return False
				End If
			Finally
				db.pool.unfix(pg)
			End Try
			Return True
		End Function

		Friend Shared Function allocate(db As DatabaseImpl, root As Integer, type As ClassDescriptor.FieldType, ins As OldBtreeKey) As Integer
			Dim pageId As Integer = db.allocatePage()
			Dim pg As Page = db.putPage(pageId)
			setnItems(pg, 1)
			If type = ClassDescriptor.FieldType.tpString Then
				Dim sval As Char() = DirectCast(ins.key.oval, Char())
				Dim len As Integer = sval.Length
				setSize(pg, len * 2)
				setKeyStrOffs(pg, 0, keySpace - len * 2)
				setKeyStrSize(pg, 0, len)
				setKeyStrOid(pg, 0, ins.oid)
				setKeyStrOid(pg, 1, root)
				setKeyStrChars(pg, keySpace - len * 2, sval)
			ElseIf type = ClassDescriptor.FieldType.tpArrayOfByte Then
				Dim bval As Byte() = DirectCast(ins.key.oval, Byte())
				Dim len As Integer = bval.Length
				setSize(pg, len)
				setKeyStrOffs(pg, 0, keySpace - len)
				setKeyStrSize(pg, 0, len)
				setKeyStrOid(pg, 0, ins.oid)
				setKeyStrOid(pg, 1, root)
				setKeyBytes(pg, keySpace - len, bval)
			Else
				ins.pack(pg, 0)
				setReference(pg, maxItems - 2, root)
			End If
			db.pool.unfix(pg)
			Return pageId
		End Function

		Friend Shared Sub memcpy(dst_pg As Page, dst_idx As Integer, src_pg As Page, src_idx As Integer, len As Integer, itemSize As Integer)
			Array.Copy(src_pg.data, firstKeyOffs + src_idx * itemSize, dst_pg.data, firstKeyOffs + dst_idx * itemSize, len * itemSize)
		End Sub

		Friend Shared Function insert(db As DatabaseImpl, pageId As Integer, tree As OldBtree, ins As OldBtreeKey, height As Integer, unique As Boolean, _
			overwrite As Boolean) As OldBtreeResult
			Dim pg As Page = db.getPage(pageId)
			Dim result As OldBtreeResult
			Dim l As Integer = 0, n As Integer = getnItems(pg), r As Integer = n
			Try
				If tree.FieldType = ClassDescriptor.FieldType.tpString Then
					While l < r
						Dim i As Integer = (l + r) >> 1
						If compareStr(ins.key, pg, i) > 0 Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(l = r)
					If System.Threading.Interlocked.Decrement(height) <> 0 Then
						result = insert(db, getKeyStrOid(pg, r), tree, ins, height, unique, _
							overwrite)
						Debug.Assert(result <> OldBtreeResult.NotFound)
						If result <> OldBtreeResult.Overflow Then
							Return result
						End If
					ElseIf r < n AndAlso compareStr(ins.key, pg, r) = 0 Then
						If overwrite Then
							db.pool.unfix(pg)
							pg = Nothing
							pg = db.putPage(pageId)
							setKeyStrOid(pg, r, ins.oid)
							Return OldBtreeResult.Overwrite
						ElseIf unique Then
							Return OldBtreeResult.Duplicate
						End If
					End If
					db.pool.unfix(pg)
					pg = Nothing
					pg = db.putPage(pageId)
					Return insertStrKey(db, pg, r, ins, height)
				ElseIf tree.FieldType = ClassDescriptor.FieldType.tpArrayOfByte Then
					While l < r
						Dim i As Integer = (l + r) >> 1
						If tree.compareByteArrays(ins.key, pg, i) > 0 Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(l = r)
					If System.Threading.Interlocked.Decrement(height) <> 0 Then
						result = insert(db, getKeyStrOid(pg, r), tree, ins, height, unique, _
							overwrite)
						Debug.Assert(result <> OldBtreeResult.NotFound)
						If result <> OldBtreeResult.Overflow Then
							Return result
						End If
					ElseIf r < n AndAlso tree.compareByteArrays(ins.key, pg, r) = 0 Then
						If overwrite Then
							db.pool.unfix(pg)
							pg = Nothing
							pg = db.putPage(pageId)
							setKeyStrOid(pg, r, ins.oid)
							Return OldBtreeResult.Overwrite
						ElseIf unique Then
							Return OldBtreeResult.Duplicate
						End If
					End If
					db.pool.unfix(pg)
					pg = Nothing
					pg = db.putPage(pageId)
					Return insertByteArrayKey(db, pg, r, ins, height)
				Else
					While l < r
						Dim i As Integer = (l + r) >> 1
						If compare(ins.key, pg, i) > 0 Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(l = r)
					' insert before e[r] 

					If System.Threading.Interlocked.Decrement(height) <> 0 Then
						result = insert(db, getReference(pg, maxItems - r - 1), tree, ins, height, unique, _
							overwrite)
						Debug.Assert(result <> OldBtreeResult.NotFound)
						If result <> OldBtreeResult.Overflow Then
							Return result
						End If
						n += 1
					ElseIf r < n AndAlso compare(ins.key, pg, r) = 0 Then
						If overwrite Then
							db.pool.unfix(pg)
							pg = Nothing
							pg = db.putPage(pageId)
							setReference(pg, maxItems - r - 1, ins.oid)
							Return OldBtreeResult.Overwrite
						ElseIf unique Then
							Return OldBtreeResult.Duplicate
						End If
					End If
					db.pool.unfix(pg)
					pg = Nothing
					pg = db.putPage(pageId)
					Dim itemSize As Integer = ClassDescriptor.Sizeof(CInt(tree.FieldType))
					Dim max As Integer = keySpace \ (4 + itemSize)
					If n < max Then
						memcpy(pg, r + 1, pg, r, n - r, itemSize)
						memcpy(pg, maxItems - n - 1, pg, maxItems - n, n - r, 4)
						ins.pack(pg, r)
						setnItems(pg, getnItems(pg) + 1)
						Return OldBtreeResult.Done
					Else
						' page is full then divide page 

						pageId = db.allocatePage()
						Dim b As Page = db.putPage(pageId)
						Debug.Assert(n = max)
						Dim m As Integer = max \ 2
						If r < m Then
							memcpy(b, 0, pg, 0, r, itemSize)
							memcpy(b, r + 1, pg, r, m - r - 1, itemSize)
							memcpy(pg, 0, pg, m - 1, max - m + 1, itemSize)
							memcpy(b, maxItems - r, pg, maxItems - r, r, 4)
							ins.pack(b, r)
							memcpy(b, maxItems - m, pg, maxItems - m + 1, m - r - 1, 4)
							memcpy(pg, maxItems - max + m - 1, pg, maxItems - max, max - m + 1, 4)
						Else
							memcpy(b, 0, pg, 0, m, itemSize)
							memcpy(pg, 0, pg, m, r - m, itemSize)
							memcpy(pg, r - m + 1, pg, r, max - r, itemSize)
							memcpy(b, maxItems - m, pg, maxItems - m, m, 4)
							memcpy(pg, maxItems - r + m, pg, maxItems - r, r - m, 4)
							ins.pack(pg, r - m)
							memcpy(pg, maxItems - max + m - 1, pg, maxItems - max, max - r, 4)
						End If
						ins.oid = pageId
						ins.extract(b, firstKeyOffs + (m - 1) * itemSize, tree.FieldType)
						If height = 0 Then
							setnItems(pg, max - m + 1)
							setnItems(b, m)
						Else
							setnItems(pg, max - m)
							setnItems(b, m - 1)
						End If
						db.pool.unfix(b)
						Return OldBtreeResult.Overflow
					End If
				End If
			Finally
				If pg IsNot Nothing Then
					db.pool.unfix(pg)
				End If
			End Try
		End Function

		Friend Shared Function insertStrKey(db As DatabaseImpl, pg As Page, r As Integer, ins As OldBtreeKey, height As Integer) As OldBtreeResult
			Dim nItems As Integer = getnItems(pg)
			Dim size As Integer = getSize(pg)
			Dim n As Integer = If((height <> 0), nItems + 1, nItems)
			' insert before e[r]
			Dim sval As Char() = DirectCast(ins.key.oval, Char())
			Dim len As Integer = sval.Length
			If size + len * 2 + (n + 1) * strKeySize <= keySpace Then
				memcpy(pg, r + 1, pg, r, n - r, strKeySize)
				size += len * 2
				setKeyStrOffs(pg, r, keySpace - size)
				setKeyStrSize(pg, r, len)
				setKeyStrOid(pg, r, ins.oid)
				setKeyStrChars(pg, keySpace - size, sval)
				nItems += 1
			Else
				' page is full then divide page
				Dim pageId As Integer = db.allocatePage()
				Dim b As Page = db.putPage(pageId)
				Dim moved As Integer = 0
				Dim inserted As Integer = len * 2 + strKeySize
				Dim prevDelta As Integer = (1 << 31) + 1

				Dim bn As Integer = 0, i As Integer = 0
				While True
					Dim addSize As Integer, subSize As Integer
					Dim j As Integer = nItems - i - 1
					Dim keyLen As Integer = getKeyStrSize(pg, i)
					If bn = r Then
						keyLen = len
						inserted = 0
						addSize = len
						If height = 0 Then
							subSize = 0
							j += 1
						Else
							subSize = getKeyStrSize(pg, i)
						End If
					Else
						addSize = InlineAssignHelper(subSize, keyLen)
						If height <> 0 Then
							If i + 1 <> r Then
								subSize += getKeyStrSize(pg, i + 1)
								j -= 1
							Else
								inserted = 0
							End If
						End If
					End If
					Dim delta As Integer = (moved + addSize * 2 + (bn + 1) * strKeySize) - (j * strKeySize + size - subSize * 2 + inserted)
					If delta >= -prevDelta Then
						If height = 0 Then
							ins.getStr(b, bn - 1)
						Else
							Debug.Assert(moved + (bn + 1) * strKeySize <= keySpace, "String fits in the B-Tree page")
							If bn <> r Then
								ins.getStr(pg, i)
								setKeyStrOid(b, bn, getKeyStrOid(pg, i))
								size -= keyLen * 2
								i += 1
							Else
								setKeyStrOid(b, bn, ins.oid)
							End If
						End If
						nItems = compactifyStrings(pg, i)
						If bn < r OrElse (bn = r AndAlso height = 0) Then
							memcpy(pg, r - i + 1, pg, r - i, n - r, strKeySize)
							size += len * 2
							nItems += 1
							Debug.Assert(size + (n - i + 1) * strKeySize <= keySpace, "String fits in the B-Tree page")
							setKeyStrOffs(pg, r - i, keySpace - size)
							setKeyStrSize(pg, r - i, len)
							setKeyStrOid(pg, r - i, ins.oid)
							setKeyStrChars(pg, keySpace - size, sval)
						End If
						setnItems(b, bn)
						setSize(b, moved)
						setSize(pg, size)
						setnItems(pg, nItems)
						ins.oid = pageId
						db.pool.unfix(b)
						Return OldBtreeResult.Overflow
					End If
					moved += keyLen * 2
					prevDelta = delta
					Debug.Assert(moved + (bn + 1) * strKeySize <= keySpace, "String fits in the B-Tree page")
					setKeyStrSize(b, bn, keyLen)
					setKeyStrOffs(b, bn, keySpace - moved)
					If bn = r Then
						setKeyStrOid(b, bn, ins.oid)
						setKeyStrChars(b, keySpace - moved, sval)
					Else
						setKeyStrOid(b, bn, getKeyStrOid(pg, i))
						memcpy(b, keySpace - moved, pg, getKeyStrOffs(pg, i), keyLen * 2, 1)
						size -= keyLen * 2
						i += 1
					End If
					bn += 1
				End While
			End If
			setnItems(pg, nItems)
			setSize(pg, size)
			Return If(size + strKeySize * (nItems + 1) < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
		End Function

		Friend Shared Function insertByteArrayKey(db As DatabaseImpl, pg As Page, r As Integer, ins As OldBtreeKey, height As Integer) As OldBtreeResult
			Dim nItems As Integer = getnItems(pg)
			Dim size As Integer = getSize(pg)
			Dim n As Integer = If((height <> 0), nItems + 1, nItems)
			' insert before e[r]
			Dim bval As Byte() = DirectCast(ins.key.oval, Byte())
			Dim len As Integer = bval.Length
			If size + len + (n + 1) * strKeySize <= keySpace Then
				memcpy(pg, r + 1, pg, r, n - r, strKeySize)
				size += len
				setKeyStrOffs(pg, r, keySpace - size)
				setKeyStrSize(pg, r, len)
				setKeyStrOid(pg, r, ins.oid)
				setKeyBytes(pg, keySpace - size, bval)
				nItems += 1
			Else
				' page is full then divide page
				Dim pageId As Integer = db.allocatePage()
				Dim b As Page = db.putPage(pageId)
				Dim moved As Integer = 0
				Dim inserted As Integer = len + strKeySize
				Dim prevDelta As Integer = (1 << 31) + 1

				Dim bn As Integer = 0, i As Integer = 0
				While True
					Dim addSize As Integer, subSize As Integer
					Dim j As Integer = nItems - i - 1
					Dim keyLen As Integer = getKeyStrSize(pg, i)
					If bn = r Then
						keyLen = len
						inserted = 0
						addSize = len
						If height = 0 Then
							subSize = 0
							j += 1
						Else
							subSize = getKeyStrSize(pg, i)
						End If
					Else
						addSize = InlineAssignHelper(subSize, keyLen)
						If height <> 0 Then
							If i + 1 <> r Then
								subSize += getKeyStrSize(pg, i + 1)
								j -= 1
							Else
								inserted = 0
							End If
						End If
					End If
					Dim delta As Integer = (moved + addSize + (bn + 1) * strKeySize) - (j * strKeySize + size - subSize + inserted)
					If delta >= -prevDelta Then
						If height = 0 Then
							ins.getByteArray(b, bn - 1)
						Else
							Debug.Assert(moved + (bn + 1) * strKeySize <= keySpace, "String fits in the B-Tree page")
							If bn <> r Then
								ins.getByteArray(pg, i)
								setKeyStrOid(b, bn, getKeyStrOid(pg, i))
								size -= keyLen
								i += 1
							Else
								setKeyStrOid(b, bn, ins.oid)
							End If
						End If
						nItems = compactifyByteArrays(pg, i)
						If bn < r OrElse (bn = r AndAlso height = 0) Then
							memcpy(pg, r - i + 1, pg, r - i, n - r, strKeySize)
							size += len
							nItems += 1
							Debug.Assert(size + (n - i + 1) * strKeySize <= keySpace, "String fits in the B-Tree page")
							setKeyStrOffs(pg, r - i, keySpace - size)
							setKeyStrSize(pg, r - i, len)
							setKeyStrOid(pg, r - i, ins.oid)
							setKeyBytes(pg, keySpace - size, bval)
						End If
						setnItems(b, bn)
						setSize(b, moved)
						setSize(pg, size)
						setnItems(pg, nItems)
						ins.oid = pageId
						db.pool.unfix(b)
						Return OldBtreeResult.Overflow
					End If
					moved += keyLen
					prevDelta = delta
					Debug.Assert(moved + (bn + 1) * strKeySize <= keySpace, "String fits in the B-Tree page")
					setKeyStrSize(b, bn, keyLen)
					setKeyStrOffs(b, bn, keySpace - moved)
					If bn = r Then
						setKeyStrOid(b, bn, ins.oid)
						setKeyBytes(b, keySpace - moved, bval)
					Else
						setKeyStrOid(b, bn, getKeyStrOid(pg, i))
						memcpy(b, keySpace - moved, pg, getKeyStrOffs(pg, i), keyLen, 1)
						size -= keyLen
						i += 1
					End If
					bn += 1
				End While
			End If
			setnItems(pg, nItems)
			setSize(pg, size)
			Return If(size + strKeySize * (nItems + 1) < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
		End Function

		Friend Shared Function compactifyStrings(pg As Page, m As Integer) As Integer
			Dim i As Integer, j As Integer, offs As Integer, len As Integer, n As Integer = getnItems(pg)
			Dim size As Integer() = New Integer(keySpace \ 2) {}
			Dim index As Integer() = New Integer(keySpace \ 2) {}
			If m = 0 Then
				Return n
			End If

			Dim nZeroLengthStrings As Integer = 0
			If m < 0 Then
				m = -m
				For i = 0 To n - m - 1
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i) >> 1
						size(offs + len) = len
						index(offs + len) = i
					Else
						nZeroLengthStrings += 1
					End If
				Next
				While i < n
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i) >> 1
						size(offs + len) = len
						index(offs + len) = -1
					End If
					i += 1
				End While
			Else
				For i = 0 To m - 1
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i) >> 1
						size(offs + len) = len
						index(offs + len) = -1
					End If
				Next
				While i < n
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i) >> 1
						size(offs + len) = len
						index(offs + len) = i - m
					Else
						nZeroLengthStrings += 1
					End If
					setKeyStrOid(pg, i - m, getKeyStrOid(pg, i))
					setKeyStrSize(pg, i - m, len)
					i += 1
				End While
				setKeyStrOid(pg, i - m, getKeyStrOid(pg, i))
			End If
			Dim nItems As Integer = n -= m
			n -= nZeroLengthStrings
			offs = keySpace \ 2
			i = offs
			While n <> 0
				len = size(i)
				j = index(i)
				If j >= 0 Then
					offs -= len
					n -= 1
					setKeyStrOffs(pg, j, offs * 2)
					If offs <> i - len Then
						memcpy(pg, offs, pg, i - len, len, 2)
					End If
				End If
				i -= len
			End While
			Return nItems
		End Function

		Friend Shared Function compactifyByteArrays(pg As Page, m As Integer) As Integer
			Dim i As Integer, j As Integer, offs As Integer, len As Integer, n As Integer = getnItems(pg)
			Dim size As Integer() = New Integer(keySpace) {}
			Dim index As Integer() = New Integer(keySpace) {}
			If m = 0 Then
				Return n
			End If

			Dim nZeroLengthArrays As Integer = 0
			If m < 0 Then
				m = -m
				For i = 0 To n - m - 1
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i)
						size(offs + len) = len
						index(offs + len) = i
					Else
						nZeroLengthArrays += 1
					End If
				Next
				While i < n
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i)
						size(offs + len) = len
						index(offs + len) = -1
					End If
					i += 1
				End While
			Else
				For i = 0 To m - 1
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i)
						size(offs + len) = len
						index(offs + len) = -1
					End If
				Next
				While i < n
					len = getKeyStrSize(pg, i)
					If len <> 0 Then
						offs = getKeyStrOffs(pg, i)
						size(offs + len) = len
						index(offs + len) = i - m
					Else
						nZeroLengthArrays += 1
					End If
					setKeyStrOid(pg, i - m, getKeyStrOid(pg, i))
					setKeyStrSize(pg, i - m, len)
					i += 1
				End While
				setKeyStrOid(pg, i - m, getKeyStrOid(pg, i))
			End If
			Dim nItems As Integer = n -= m
			n -= nZeroLengthArrays
			offs = keySpace
			i = offs
			While n <> 0
				len = size(i)
				j = index(i)
				If j >= 0 Then
					offs -= len
					n -= 1
					setKeyStrOffs(pg, j, offs)
					If offs <> i - len Then
						memcpy(pg, offs, pg, i - len, len, 1)
					End If
				End If
				i -= len
			End While
			Return nItems
		End Function

		Friend Shared Function removeStrKey(pg As Page, r As Integer) As OldBtreeResult
			Dim len As Integer = getKeyStrSize(pg, r) * 2
			Dim offs As Integer = getKeyStrOffs(pg, r)
			Dim size As Integer = getSize(pg)
			Dim nItems As Integer = getnItems(pg)
			If (nItems + 1) * strKeySize >= keySpace Then
				memcpy(pg, r, pg, r + 1, nItems - r - 1, strKeySize)
			Else
				memcpy(pg, r, pg, r + 1, nItems - r, strKeySize)
			End If

			If len <> 0 Then
				memcpy(pg, keySpace - size + len, pg, keySpace - size, size - keySpace + offs, 1)
				Dim i As Integer = nItems
				While System.Threading.Interlocked.Decrement(i) >= 0
					If getKeyStrOffs(pg, i) < offs Then
						setKeyStrOffs(pg, i, getKeyStrOffs(pg, i) + len)
					End If
				End While
				setSize(pg, size -= len)
			End If
			setnItems(pg, nItems - 1)
			Return If(size + strKeySize * nItems < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
		End Function

		Friend Shared Function removeByteArrayKey(pg As Page, r As Integer) As OldBtreeResult
			Dim len As Integer = getKeyStrSize(pg, r)
			Dim offs As Integer = getKeyStrOffs(pg, r)
			Dim size As Integer = getSize(pg)
			Dim nItems As Integer = getnItems(pg)
			If (nItems + 1) * strKeySize >= keySpace Then
				memcpy(pg, r, pg, r + 1, nItems - r - 1, strKeySize)
			Else
				memcpy(pg, r, pg, r + 1, nItems - r, strKeySize)
			End If

			If len <> 0 Then
				memcpy(pg, keySpace - size + len, pg, keySpace - size, size - keySpace + offs, 1)
				Dim i As Integer = nItems
				While System.Threading.Interlocked.Decrement(i) >= 0
					If getKeyStrOffs(pg, i) < offs Then
						setKeyStrOffs(pg, i, getKeyStrOffs(pg, i) + len)
					End If
				End While
				setSize(pg, size -= len)
			End If
			setnItems(pg, nItems - 1)
			Return If(size + strKeySize * nItems < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
		End Function

		Friend Shared Function replaceStrKey(db As DatabaseImpl, pg As Page, r As Integer, ins As OldBtreeKey, height As Integer) As OldBtreeResult
			ins.oid = getKeyStrOid(pg, r)
			removeStrKey(pg, r)
			Return insertStrKey(db, pg, r, ins, height)
		End Function

		Friend Shared Function replaceByteArrayKey(db As DatabaseImpl, pg As Page, r As Integer, ins As OldBtreeKey, height As Integer) As OldBtreeResult
			ins.oid = getKeyStrOid(pg, r)
			removeByteArrayKey(pg, r)
			Return insertByteArrayKey(db, pg, r, ins, height)
		End Function

		Friend Shared Function handlePageUnderflow(db As DatabaseImpl, pg As Page, r As Integer, type As ClassDescriptor.FieldType, [rem] As OldBtreeKey, height As Integer) As OldBtreeResult
			Dim nItems As Integer = getnItems(pg)
			If type = ClassDescriptor.FieldType.tpString Then
				Dim a As Page = db.putPage(getKeyStrOid(pg, r))
				Dim an As Integer = getnItems(a)
				If r < nItems Then
					' exists greater page
					Dim b As Page = db.getPage(getKeyStrOid(pg, r + 1))
					Dim bn As Integer = getnItems(b)
					Dim merged_size As Integer = (an + bn) * strKeySize + getSize(a) + getSize(b)
					If height <> 1 Then
						merged_size += getKeyStrSize(pg, r) * 2 + strKeySize * 2
					End If
					If merged_size > keySpace Then
						' reallocation of nodes between pages a and b
						Dim i As Integer, j As Integer, k As Integer
						db.pool.unfix(b)
						b = db.putPage(getKeyStrOid(pg, r + 1))
						Dim size_a As Integer = getSize(a)
						Dim size_b As Integer = getSize(b)
						Dim addSize As Integer, subSize As Integer
						If height <> 1 Then
							addSize = getKeyStrSize(pg, r)
							subSize = getKeyStrSize(b, 0)
						Else
							addSize = InlineAssignHelper(subSize, getKeyStrSize(b, 0))
						End If
						i = 0
						Dim prevDelta As Integer = (an * strKeySize + size_a) - (bn * strKeySize + size_b)
						While True
							i += 1
							Dim delta As Integer = ((an + i) * strKeySize + size_a + addSize * 2) - ((bn - i) * strKeySize + size_b - subSize * 2)
							If delta >= 0 Then
								If delta >= -prevDelta Then
									i -= 1
								End If
								Exit While
							End If
							size_a += addSize * 2
							size_b -= subSize * 2
							prevDelta = delta
							If height <> 1 Then
								addSize = subSize
								subSize = getKeyStrSize(b, i)
							Else
								addSize = InlineAssignHelper(subSize, getKeyStrSize(b, i))
							End If
						End While
						Dim result As OldBtreeResult = OldBtreeResult.Done
						If i > 0 Then
							k = i
							If height <> 1 Then
								Dim len As Integer = getKeyStrSize(pg, r)
								setSize(a, getSize(a) + len * 2)
								setKeyStrOffs(a, an, keySpace - getSize(a))
								setKeyStrSize(a, an, len)
								memcpy(a, getKeyStrOffs(a, an), pg, getKeyStrOffs(pg, r), len * 2, 1)
								k -= 1
								an += 1
								setKeyStrOid(a, an + k, getKeyStrOid(b, k))
								setSize(b, getSize(b) - getKeyStrSize(b, k) * 2)
							End If
							For j = 0 To k - 1
								Dim len As Integer = getKeyStrSize(b, j)
								setSize(a, getSize(a) + len * 2)
								setSize(b, getSize(b) - len * 2)
								setKeyStrOffs(a, an, keySpace - getSize(a))
								setKeyStrSize(a, an, len)
								setKeyStrOid(a, an, getKeyStrOid(b, j))
								memcpy(a, getKeyStrOffs(a, an), b, getKeyStrOffs(b, j), len * 2, 1)
								an += 1
							Next
							[rem].getStr(b, i - 1)
							result = replaceStrKey(db, pg, r, [rem], height)
							setnItems(a, an)
							setnItems(b, compactifyStrings(b, i))
						End If
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return result
					Else
						' merge page b to a
						If height <> 1 Then
							Dim r_len As Integer = getKeyStrSize(pg, r)
							setKeyStrSize(a, an, r_len)
							setSize(a, getSize(a) + r_len * 2)
							setKeyStrOffs(a, an, keySpace - getSize(a))
							memcpy(a, getKeyStrOffs(a, an), pg, getKeyStrOffs(pg, r), r_len * 2, 1)
							an += 1
							setKeyStrOid(a, an + bn, getKeyStrOid(b, bn))
						End If
						Dim i As Integer = 0
						While i < bn
							setKeyStrSize(a, an, getKeyStrSize(b, i))
							setKeyStrOffs(a, an, getKeyStrOffs(b, i) - getSize(a))
							setKeyStrOid(a, an, getKeyStrOid(b, i))
							i += 1
							an += 1
						End While
						setSize(a, getSize(a) + getSize(b))
						setnItems(a, an)
						memcpy(a, keySpace - getSize(a), b, keySpace - getSize(b), getSize(b), 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						db.freePage(getKeyStrOid(pg, r + 1))
						setKeyStrOid(pg, r + 1, getKeyStrOid(pg, r))
						Return removeStrKey(pg, r)
					End If
				Else
					' page b is before a
					Dim b As Page = db.getPage(getKeyStrOid(pg, r - 1))
					Dim bn As Integer = getnItems(b)
					Dim merged_size As Integer = (an + bn) * strKeySize + getSize(a) + getSize(b)
					If height <> 1 Then
						merged_size += getKeyStrSize(pg, r - 1) * 2 + strKeySize * 2
					End If

					If merged_size > keySpace Then
						' reallocation of nodes between pages a and b
						Dim i As Integer, j As Integer, k As Integer, len As Integer
						db.pool.unfix(b)
						b = db.putPage(getKeyStrOid(pg, r - 1))
						Dim size_a As Integer = getSize(a)
						Dim size_b As Integer = getSize(b)
						Dim addSize As Integer, subSize As Integer
						If height <> 1 Then
							addSize = getKeyStrSize(pg, r - 1)
							subSize = getKeyStrSize(b, bn - 1)
						Else
							addSize = InlineAssignHelper(subSize, getKeyStrSize(b, bn - 1))
						End If
						i = 0
						Dim prevDelta As Integer = (an * strKeySize + size_a) - (bn * strKeySize + size_b)
						While True
							i += 1
							Dim delta As Integer = ((an + i) * strKeySize + size_a + addSize * 2) - ((bn - i) * strKeySize + size_b - subSize * 2)
							If delta >= 0 Then
								If delta >= -prevDelta Then
									i -= 1
								End If
								Exit While
							End If
							prevDelta = delta
							size_a += addSize * 2
							size_b -= subSize * 2
							If height <> 1 Then
								addSize = subSize
								subSize = getKeyStrSize(b, bn - i - 1)
							Else
								addSize = InlineAssignHelper(subSize, getKeyStrSize(b, bn - i - 1))
							End If
						End While
						Dim result As OldBtreeResult = OldBtreeResult.Done
						If i > 0 Then
							k = i
							Debug.Assert(i < bn)
							If height <> 1 Then
								setSize(b, getSize(b) - getKeyStrSize(b, bn - k) * 2)
								memcpy(a, i, a, 0, an + 1, strKeySize)
								k -= 1
								setKeyStrOid(a, k, getKeyStrOid(b, bn))
								len = getKeyStrSize(pg, r - 1)
								setKeyStrSize(a, k, len)
								setSize(a, getSize(a) + len * 2)
								setKeyStrOffs(a, k, keySpace - getSize(a))
								memcpy(a, getKeyStrOffs(a, k), pg, getKeyStrOffs(pg, r - 1), len * 2, 1)
							Else
								memcpy(a, i, a, 0, an, strKeySize)
							End If
							For j = 0 To k - 1
								len = getKeyStrSize(b, bn - k + j)
								setSize(a, getSize(a) + len * 2)
								setSize(b, getSize(b) - len * 2)
								setKeyStrOffs(a, j, keySpace - getSize(a))
								setKeyStrSize(a, j, len)
								setKeyStrOid(a, j, getKeyStrOid(b, bn - k + j))
								memcpy(a, getKeyStrOffs(a, j), b, getKeyStrOffs(b, bn - k + j), len * 2, 1)
							Next
							an += i
							setnItems(a, an)
							[rem].getStr(b, bn - k - 1)
							result = replaceStrKey(db, pg, r - 1, [rem], height)
							setnItems(b, compactifyStrings(b, -i))
						End If
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return result
					Else
						' merge page b to a
						If height <> 1 Then
							memcpy(a, bn + 1, a, 0, an + 1, strKeySize)
							Dim len As Integer = getKeyStrSize(pg, r - 1)
							setKeyStrSize(a, bn, len)
							setSize(a, getSize(a) + len * 2)
							setKeyStrOffs(a, bn, keySpace - getSize(a))
							setKeyStrOid(a, bn, getKeyStrOid(b, bn))
							memcpy(a, getKeyStrOffs(a, bn), pg, getKeyStrOffs(pg, r - 1), len * 2, 1)
							an += 1
						Else
							memcpy(a, bn, a, 0, an, strKeySize)
						End If
						For i As Integer = 0 To bn - 1
							setKeyStrOid(a, i, getKeyStrOid(b, i))
							setKeyStrSize(a, i, getKeyStrSize(b, i))
							setKeyStrOffs(a, i, getKeyStrOffs(b, i) - getSize(a))
						Next
						an += bn
						setnItems(a, an)
						setSize(a, getSize(a) + getSize(b))
						memcpy(a, keySpace - getSize(a), b, keySpace - getSize(b), getSize(b), 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						db.freePage(getKeyStrOid(pg, r - 1))
						Return removeStrKey(pg, r - 1)
					End If
				End If
			ElseIf type = ClassDescriptor.FieldType.tpArrayOfByte Then
				Dim a As Page = db.putPage(getKeyStrOid(pg, r))
				Dim an As Integer = getnItems(a)
				If r < nItems Then
					' exists greater page
					Dim b As Page = db.getPage(getKeyStrOid(pg, r + 1))
					Dim bn As Integer = getnItems(b)
					Dim merged_size As Integer = (an + bn) * strKeySize + getSize(a) + getSize(b)
					If height <> 1 Then
						merged_size += getKeyStrSize(pg, r) + strKeySize * 2
					End If
					If merged_size > keySpace Then
						' reallocation of nodes between pages a and b
						Dim i As Integer, j As Integer, k As Integer
						db.pool.unfix(b)
						b = db.putPage(getKeyStrOid(pg, r + 1))
						Dim size_a As Integer = getSize(a)
						Dim size_b As Integer = getSize(b)
						Dim addSize As Integer, subSize As Integer
						If height <> 1 Then
							addSize = getKeyStrSize(pg, r)
							subSize = getKeyStrSize(b, 0)
						Else
							addSize = InlineAssignHelper(subSize, getKeyStrSize(b, 0))
						End If
						i = 0
						Dim prevDelta As Integer = (an * strKeySize + size_a) - (bn * strKeySize + size_b)
						While True
							i += 1
							Dim delta As Integer = ((an + i) * strKeySize + size_a + addSize) - ((bn - i) * strKeySize + size_b - subSize)
							If delta >= 0 Then
								If delta >= -prevDelta Then
									i -= 1
								End If

								Exit While
							End If
							size_a += addSize
							size_b -= subSize
							prevDelta = delta
							If height <> 1 Then
								addSize = subSize
								subSize = getKeyStrSize(b, i)
							Else
								addSize = InlineAssignHelper(subSize, getKeyStrSize(b, i))
							End If
						End While
						Dim result As OldBtreeResult = OldBtreeResult.Done
						If i > 0 Then
							k = i
							If height <> 1 Then
								Dim len As Integer = getKeyStrSize(pg, r)
								setSize(a, getSize(a) + len)
								setKeyStrOffs(a, an, keySpace - getSize(a))
								setKeyStrSize(a, an, len)
								memcpy(a, getKeyStrOffs(a, an), pg, getKeyStrOffs(pg, r), len, 1)
								k -= 1
								an += 1
								setKeyStrOid(a, an + k, getKeyStrOid(b, k))
								setSize(b, getSize(b) - getKeyStrSize(b, k))
							End If
							For j = 0 To k - 1
								Dim len As Integer = getKeyStrSize(b, j)
								setSize(a, getSize(a) + len)
								setSize(b, getSize(b) - len)
								setKeyStrOffs(a, an, keySpace - getSize(a))
								setKeyStrSize(a, an, len)
								setKeyStrOid(a, an, getKeyStrOid(b, j))
								memcpy(a, getKeyStrOffs(a, an), b, getKeyStrOffs(b, j), len, 1)
								an += 1
							Next
							[rem].getByteArray(b, i - 1)
							result = replaceByteArrayKey(db, pg, r, [rem], height)
							setnItems(a, an)
							setnItems(b, compactifyByteArrays(b, i))
						End If
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return result
					Else
						' merge page b to a
						If height <> 1 Then
							Dim r_len As Integer = getKeyStrSize(pg, r)
							setKeyStrSize(a, an, r_len)
							setSize(a, getSize(a) + r_len)
							setKeyStrOffs(a, an, keySpace - getSize(a))
							memcpy(a, getKeyStrOffs(a, an), pg, getKeyStrOffs(pg, r), r_len, 1)
							an += 1
							setKeyStrOid(a, an + bn, getKeyStrOid(b, bn))
						End If
						Dim i As Integer = 0
						While i < bn
							setKeyStrSize(a, an, getKeyStrSize(b, i))
							setKeyStrOffs(a, an, getKeyStrOffs(b, i) - getSize(a))
							setKeyStrOid(a, an, getKeyStrOid(b, i))
							i += 1
							an += 1
						End While
						setSize(a, getSize(a) + getSize(b))
						setnItems(a, an)
						memcpy(a, keySpace - getSize(a), b, keySpace - getSize(b), getSize(b), 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						db.freePage(getKeyStrOid(pg, r + 1))
						setKeyStrOid(pg, r + 1, getKeyStrOid(pg, r))
						Return removeByteArrayKey(pg, r)
					End If
				Else
					' page b is before a
					Dim b As Page = db.getPage(getKeyStrOid(pg, r - 1))
					Dim bn As Integer = getnItems(b)
					Dim merged_size As Integer = (an + bn) * strKeySize + getSize(a) + getSize(b)
					If height <> 1 Then
						merged_size += getKeyStrSize(pg, r - 1) + strKeySize * 2
					End If

					If merged_size > keySpace Then
						' reallocation of nodes between pages a and b
						Dim i As Integer, j As Integer, k As Integer, len As Integer
						db.pool.unfix(b)
						b = db.putPage(getKeyStrOid(pg, r - 1))
						Dim size_a As Integer = getSize(a)
						Dim size_b As Integer = getSize(b)
						Dim addSize As Integer, subSize As Integer
						If height <> 1 Then
							addSize = getKeyStrSize(pg, r - 1)
							subSize = getKeyStrSize(b, bn - 1)
						Else
							addSize = InlineAssignHelper(subSize, getKeyStrSize(b, bn - 1))
						End If
						i = 0
						Dim prevDelta As Integer = (an * strKeySize + size_a) - (bn * strKeySize + size_b)
						While True
							i += 1
							Dim delta As Integer = ((an + i) * strKeySize + size_a + addSize) - ((bn - i) * strKeySize + size_b - subSize)
							If delta >= 0 Then
								If delta >= -prevDelta Then
									i -= 1
								End If
								Exit While
							End If
							prevDelta = delta
							size_a += addSize
							size_b -= subSize
							If height <> 1 Then
								addSize = subSize
								subSize = getKeyStrSize(b, bn - i - 1)
							Else
								addSize = InlineAssignHelper(subSize, getKeyStrSize(b, bn - i - 1))
							End If
						End While
						Dim result As OldBtreeResult = OldBtreeResult.Done
						If i > 0 Then
							k = i
							Debug.Assert(i < bn)
							If height <> 1 Then
								setSize(b, getSize(b) - getKeyStrSize(b, bn - k))
								memcpy(a, i, a, 0, an + 1, strKeySize)
								k -= 1
								setKeyStrOid(a, k, getKeyStrOid(b, bn))
								len = getKeyStrSize(pg, r - 1)
								setKeyStrSize(a, k, len)
								setSize(a, getSize(a) + len)
								setKeyStrOffs(a, k, keySpace - getSize(a))
								memcpy(a, getKeyStrOffs(a, k), pg, getKeyStrOffs(pg, r - 1), len, 1)
							Else
								memcpy(a, i, a, 0, an, strKeySize)
							End If
							For j = 0 To k - 1
								len = getKeyStrSize(b, bn - k + j)
								setSize(a, getSize(a) + len)
								setSize(b, getSize(b) - len)
								setKeyStrOffs(a, j, keySpace - getSize(a))
								setKeyStrSize(a, j, len)
								setKeyStrOid(a, j, getKeyStrOid(b, bn - k + j))
								memcpy(a, getKeyStrOffs(a, j), b, getKeyStrOffs(b, bn - k + j), len, 1)
							Next
							an += i
							setnItems(a, an)
							[rem].getByteArray(b, bn - k - 1)
							result = replaceByteArrayKey(db, pg, r - 1, [rem], height)
							setnItems(b, compactifyByteArrays(b, -i))
						End If
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return result
					Else
						' merge page b to a
						If height <> 1 Then
							memcpy(a, bn + 1, a, 0, an + 1, strKeySize)
							Dim len As Integer = getKeyStrSize(pg, r - 1)
							setKeyStrSize(a, bn, len)
							setSize(a, getSize(a) + len)
							setKeyStrOffs(a, bn, keySpace - getSize(a))
							setKeyStrOid(a, bn, getKeyStrOid(b, bn))
							memcpy(a, getKeyStrOffs(a, bn), pg, getKeyStrOffs(pg, r - 1), len, 1)
							an += 1
						Else
							memcpy(a, bn, a, 0, an, strKeySize)
						End If
						For i As Integer = 0 To bn - 1
							setKeyStrOid(a, i, getKeyStrOid(b, i))
							setKeyStrSize(a, i, getKeyStrSize(b, i))
							setKeyStrOffs(a, i, getKeyStrOffs(b, i) - getSize(a))
						Next
						an += bn
						setnItems(a, an)
						setSize(a, getSize(a) + getSize(b))
						memcpy(a, keySpace - getSize(a), b, keySpace - getSize(b), getSize(b), 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						db.freePage(getKeyStrOid(pg, r - 1))
						Return removeByteArrayKey(pg, r - 1)
					End If
				End If
			Else
				Dim a As Page = db.putPage(getReference(pg, maxItems - r - 1))
				Dim an As Integer = getnItems(a)
				Dim itemSize As Integer = ClassDescriptor.Sizeof(CInt(type))
				If r < nItems Then
					' exists greater page
					Dim b As Page = db.getPage(getReference(pg, maxItems - r - 2))
					Dim bn As Integer = getnItems(b)
					Debug.Assert(bn >= an)
					If height <> 1 Then
						memcpy(a, an, pg, r, 1, itemSize)
						an += 1
						bn += 1
					End If
					Dim merged_size As Integer = (an + bn) * (4 + itemSize)
					If merged_size > keySpace Then
						' reallocation of nodes between pages a and b
						Dim i As Integer = bn - ((an + bn) >> 1)
						db.pool.unfix(b)
						b = db.putPage(getReference(pg, maxItems - r - 2))
						memcpy(a, an, b, 0, i, itemSize)
						memcpy(b, 0, b, i, bn - i, itemSize)
						memcpy(a, maxItems - an - i, b, maxItems - i, i, 4)
						memcpy(b, maxItems - bn + i, b, maxItems - bn, bn - i, 4)
						memcpy(pg, r, a, an + i - 1, 1, itemSize)
						setnItems(b, getnItems(b) - i)
						setnItems(a, getnItems(a) + i)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return OldBtreeResult.Done
					Else
						' merge page b to a  
						memcpy(a, an, b, 0, bn, itemSize)
						memcpy(a, maxItems - an - bn, b, maxItems - bn, bn, 4)
						db.freePage(getReference(pg, maxItems - r - 2))
						memcpy(pg, maxItems - nItems, pg, maxItems - nItems - 1, nItems - r - 1, 4)
						memcpy(pg, r, pg, r + 1, nItems - r - 1, itemSize)
						setnItems(a, getnItems(a) + bn)
						setnItems(pg, nItems - 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return If(nItems * (itemSize + 4) < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
					End If
				Else
					' page b is before a
					Dim b As Page = db.getPage(getReference(pg, maxItems - r))
					Dim bn As Integer = getnItems(b)
					Debug.Assert(bn >= an)
					If height <> 1 Then
						an += 1
						bn += 1
					End If
					Dim merged_size As Integer = (an + bn) * (4 + itemSize)
					If merged_size > keySpace Then
						' reallocation of nodes between pages a and b
						Dim i As Integer = bn - ((an + bn) >> 1)
						db.pool.unfix(b)
						b = db.putPage(getReference(pg, maxItems - r))
						memcpy(a, i, a, 0, an, itemSize)
						memcpy(a, 0, b, bn - i, i, itemSize)
						memcpy(a, maxItems - an - i, a, maxItems - an, an, 4)
						memcpy(a, maxItems - i, b, maxItems - bn, i, 4)
						If height <> 1 Then
							memcpy(a, i - 1, pg, r - 1, 1, itemSize)
						End If
						memcpy(pg, r - 1, b, bn - i - 1, 1, itemSize)
						setnItems(b, getnItems(b) - i)
						setnItems(a, getnItems(a) + i)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return OldBtreeResult.Done
					Else
						' merge page b to a
						memcpy(a, bn, a, 0, an, itemSize)
						memcpy(a, 0, b, 0, bn, itemSize)
						memcpy(a, maxItems - an - bn, a, maxItems - an, an, 4)
						memcpy(a, maxItems - bn, b, maxItems - bn, bn, 4)
						If height <> 1 Then
							memcpy(a, bn - 1, pg, r - 1, 1, itemSize)
						End If

						db.freePage(getReference(pg, maxItems - r))
						setReference(pg, maxItems - r, getReference(pg, maxItems - r - 1))
						setnItems(a, getnItems(a) + bn)
						setnItems(pg, nItems - 1)
						db.pool.unfix(a)
						db.pool.unfix(b)
						Return If(nItems * (itemSize + 4) < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
					End If
				End If
			End If
		End Function

		Friend Shared Function remove(db As DatabaseImpl, pageId As Integer, tree As OldBtree, [rem] As OldBtreeKey, height As Integer) As OldBtreeResult
			Dim pg As Page = db.getPage(pageId)
			Try
				Dim i As Integer, n As Integer = getnItems(pg), l As Integer = 0, r As Integer = n

				If tree.FieldType = ClassDescriptor.FieldType.tpString Then
					While l < r
						i = (l + r) >> 1
						If compareStr([rem].key, pg, i) > 0 Then
							l = i + 1
						Else
							r = i
						End If
					End While
					If System.Threading.Interlocked.Decrement(height) <> 0 Then
						Do
							Select Case remove(db, getKeyStrOid(pg, r), tree, [rem], height)
								Case OldBtreeResult.Underflow
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									Return handlePageUnderflow(db, pg, r, tree.FieldType, [rem], height)

								Case OldBtreeResult.Done
									Return OldBtreeResult.Done

								Case OldBtreeResult.Overflow
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									Return insertStrKey(db, pg, r, [rem], height)

							End Select
						Loop While System.Threading.Interlocked.Increment(r) <= n
					Else
						While r < n
							If compareStr([rem].key, pg, r) = 0 Then
								Dim oid As Integer = getKeyStrOid(pg, r)
								If oid = [rem].oid OrElse [rem].oid = 0 Then
									[rem].oldOid = oid
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									Return removeStrKey(pg, r)
								End If
							Else
								Exit While
							End If
							r += 1
						End While
					End If
				ElseIf tree.FieldType = ClassDescriptor.FieldType.tpArrayOfByte Then
					While l < r
						i = (l + r) >> 1
						If tree.compareByteArrays([rem].key, pg, i) > 0 Then
							l = i + 1
						Else
							r = i
						End If
					End While
					If System.Threading.Interlocked.Decrement(height) <> 0 Then
						Do
							Select Case remove(db, getKeyStrOid(pg, r), tree, [rem], height)
								Case OldBtreeResult.Underflow
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									Return handlePageUnderflow(db, pg, r, tree.FieldType, [rem], height)

								Case OldBtreeResult.Done
									Return OldBtreeResult.Done

								Case OldBtreeResult.Overflow
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									Return insertByteArrayKey(db, pg, r, [rem], height)

							End Select
						Loop While System.Threading.Interlocked.Increment(r) <= n
					Else
						While r < n
							If tree.compareByteArrays([rem].key, pg, r) = 0 Then
								Dim oid As Integer = getKeyStrOid(pg, r)
								If oid = [rem].oid OrElse [rem].oid = 0 Then
									[rem].oldOid = oid
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									Return removeByteArrayKey(pg, r)
								End If
							Else
								Exit While
							End If
							r += 1
						End While
					End If
				Else
					' scalars
					Dim itemSize As Integer = ClassDescriptor.Sizeof(CInt(tree.FieldType))
					While l < r
						i = (l + r) >> 1
						If compare([rem].key, pg, i) > 0 Then
							l = i + 1
						Else
							r = i

						End If
					End While
					If System.Threading.Interlocked.Decrement(height) = 0 Then
						Dim oid As Integer = [rem].oid
						While r < n
							If compare([rem].key, pg, r) = 0 Then
								If getReference(pg, maxItems - r - 1) = oid OrElse oid = 0 Then
									[rem].oldOid = getReference(pg, maxItems - r - 1)
									db.pool.unfix(pg)
									pg = Nothing
									pg = db.putPage(pageId)
									memcpy(pg, r, pg, r + 1, n - r - 1, itemSize)
									memcpy(pg, maxItems - n + 1, pg, maxItems - n, n - r - 1, 4)
									setnItems(pg, System.Threading.Interlocked.Decrement(n))
									Return If(n * (itemSize + 4) < keySpace \ 2, OldBtreeResult.Underflow, OldBtreeResult.Done)
								End If
							Else
								Exit While
							End If
							r += 1
						End While
						Return OldBtreeResult.NotFound
					End If
					Do
						Select Case remove(db, getReference(pg, maxItems - r - 1), tree, [rem], height)
							Case OldBtreeResult.Underflow
								db.pool.unfix(pg)
								pg = db.putPage(pageId)
								Return handlePageUnderflow(db, pg, r, tree.FieldType, [rem], height)

							Case OldBtreeResult.Done
								Return OldBtreeResult.Done

						End Select
					Loop While System.Threading.Interlocked.Increment(r) <= n
				End If
				Return OldBtreeResult.NotFound
			Finally
				If pg IsNot Nothing Then

					db.pool.unfix(pg)
				End If
			End Try
		End Function

		Friend Shared Sub purge(db As DatabaseImpl, pageId As Integer, type As ClassDescriptor.FieldType, height As Integer)
			If System.Threading.Interlocked.Decrement(height) <> 0 Then
				Dim pg As Page = db.getPage(pageId)
				Dim n As Integer = getnItems(pg) + 1
				If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
					' page of strings
					While System.Threading.Interlocked.Decrement(n) >= 0
						purge(db, getKeyStrOid(pg, n), type, height)
					End While
				Else
					While System.Threading.Interlocked.Decrement(n) >= 0
						purge(db, getReference(pg, maxItems - n - 1), type, height)
					End While
				End If
				db.pool.unfix(pg)
			End If
			db.freePage(pageId)
		End Sub

		Friend Shared Function traverseForward(db As DatabaseImpl, pageId As Integer, type As ClassDescriptor.FieldType, height As Integer, result As IPersistent(), pos As Integer) As Integer
			Dim pg As Page = db.getPage(pageId)
			Dim oid As Integer
			Try
				Dim i As Integer, n As Integer = getnItems(pg)
				If System.Threading.Interlocked.Decrement(height) <> 0 Then
					If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
						' page of strings
						For i = 0 To n
							pos = traverseForward(db, getKeyStrOid(pg, i), type, height, result, pos)
						Next
					Else
						For i = 0 To n
							pos = traverseForward(db, getReference(pg, maxItems - i - 1), type, height, result, pos)
						Next
					End If
				Else
					If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
						' page of strings
						For i = 0 To n - 1
							oid = getKeyStrOid(pg, i)
							result(System.Math.Max(System.Threading.Interlocked.Increment(pos),pos - 1)) = db.lookupObject(oid, Nothing)
						Next
					Else
						' page of scalars
						For i = 0 To n - 1
							oid = getReference(pg, maxItems - 1 - i)
							result(System.Math.Max(System.Threading.Interlocked.Increment(pos),pos - 1)) = db.lookupObject(oid, Nothing)
						Next
					End If
				End If
				Return pos
			Finally
				db.pool.unfix(pg)
			End Try
		End Function

		Friend Shared Function markPage(db As DatabaseImpl, pageId As Integer, type As ClassDescriptor.FieldType, height As Integer) As Integer
			Dim pg As Page = db.getGCPage(pageId)
			Dim nPages As Integer = 1
			Try
				Dim i As Integer, n As Integer = getnItems(pg)
				If System.Threading.Interlocked.Decrement(height) <> 0 Then
					If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
						' page of strings
						For i = 0 To n
							nPages += markPage(db, getKeyStrOid(pg, i), type, height)
						Next
					Else
						For i = 0 To n
							nPages += markPage(db, getReference(pg, maxItems - i - 1), type, height)
						Next
					End If
				Else
					If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
						' page of strings
						For i = 0 To n - 1
							db.markOid(getKeyStrOid(pg, i))
						Next
					Else
						' page of scalars
						For i = 0 To n - 1
							db.markOid(getReference(pg, maxItems - 1 - i))
						Next
					End If
				End If
			Finally
				db.pool.unfix(pg)
			End Try
			Return nPages
		End Function

		#If WITH_XML Then
		Friend Shared Sub exportPage(db As DatabaseImpl, exporter As XmlExporter, pageId As Integer, type As ClassDescriptor.FieldType, height As Integer)
			Dim pg As Page = db.getPage(pageId)
			Try
				Dim i As Integer, n As Integer = getnItems(pg)
				If System.Threading.Interlocked.Decrement(height) <> 0 Then
					If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
						' page of strings
						For i = 0 To n
							exportPage(db, exporter, getKeyStrOid(pg, i), type, height)
						Next
					Else
						For i = 0 To n
							exportPage(db, exporter, getReference(pg, maxItems - i - 1), type, height)
						Next
					End If
				Else
					If type = ClassDescriptor.FieldType.tpString OrElse type = ClassDescriptor.FieldType.tpArrayOfByte Then
						' page of strings
						For i = 0 To n - 1
							exporter.exportAssoc(getKeyStrOid(pg, i), pg.data, OldBtreePage.firstKeyOffs + OldBtreePage.getKeyStrOffs(pg, i), OldBtreePage.getKeyStrSize(pg, i), type)
						Next
					Else
						' page of scalars
						For i = 0 To n - 1
							exporter.exportAssoc(getReference(pg, maxItems - 1 - i), pg.data, OldBtreePage.firstKeyOffs + i * ClassDescriptor.Sizeof(CInt(type)), ClassDescriptor.Sizeof(CInt(type)), type)
						Next
					End If
				End If
			Finally
				db.pool.unfix(pg)
			End Try
		End Sub
		Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
			target = value
			Return value
		End Function
		#End If
	End Class
End Namespace
#End If
