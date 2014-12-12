Imports System.Collections.Generic
Imports System.Collections
Imports System.Reflection
Imports System.Diagnostics
Imports Volante
Namespace Volante.Impl

	Class TimeSeriesImpl(Of T As ITimeSeriesTick)
		Inherits PersistentResource
		Implements ITimeSeries(Of T)
		Public Sub Clear()
			For Each block As TimeSeriesBlock In index
				block.Deallocate()
			Next
			index.Clear()
		End Sub

		Public Overridable Sub CopyTo(dst As T(), i As Integer)
			For Each o As Object In Me
				dst.SetValue(o, System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1))
			Next
		End Sub

		Public Overridable ReadOnly Property IsReadOnly() As Boolean
			Get
				Return False
			End Get
		End Property

		Public Function Contains(obj As T) As Boolean
			Return Contains(New DateTime(obj.Ticks))
		End Function

		Public Function Remove(obj As T) As Boolean
			Dim t As New DateTime(obj.Ticks)
			Return Remove(t, t) <> 0
		End Function

		Public Class TimeSeriesBlock
			Inherits Persistent
			Public timestamp As Long
			Public used As Integer

			Public Ticks As T()

			Public Default Property Item(i As Integer) As T
				Get
					Return Ticks(i)
				End Get

				Set
					Ticks(i) = value
				End Set
			End Property

			Public Sub New(size As Integer)
				Ticks = New T(size - 1) {}
			End Sub

			Private Sub New()
			End Sub
		End Class

		Public Sub Add(tick As T)
			Dim time As Long = tick.Ticks
			For Each block As TimeSeriesBlock In index.Range(time - maxBlockTimeInterval, time, IterationOrder.DescentOrder)
				insertInBlock(block, tick)
				Return
			Next
			addNewBlock(tick)
		End Sub

		Private Class TimeSeriesEnumerator
			Implements IEnumerator(Of T)
			Implements IEnumerable(Of T)
			Friend Sub New(blockIterator As IEnumerator(Of TimeSeriesBlock), from As Long, till As Long)
				Me.till = till
				Me.from = from
				Me.blockIterator = blockIterator
				Reset()
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of T) Implements IEnumerable(Of T).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return GetEnumerator()
			End Function

			Public Sub Reset() Implements IEnumerator.Reset
				hasCurrent = False
				blockIterator.Reset()
				pos = -1
				While blockIterator.MoveNext()
					Dim block As TimeSeriesBlock = DirectCast(blockIterator.Current, TimeSeriesBlock)
					Dim n As Integer = block.used
					Dim l As Integer = 0, r As Integer = n
					While l < r
						Dim i As Integer = (l + r) >> 1
						If from > block(i).Ticks Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(l = r AndAlso (l = n OrElse block(l).Ticks >= from))
					If l < n Then
						pos = l
						currBlock = block
						Return
					End If
				End While
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If hasCurrent Then
					hasCurrent = False
					If System.Threading.Interlocked.Increment(pos) = currBlock.used Then
						If blockIterator.MoveNext() Then
							currBlock = DirectCast(blockIterator.Current, TimeSeriesBlock)
							pos = 0
						Else
							pos = -1
							Return False
						End If
					End If
				ElseIf pos < 0 Then
					Return False
				End If
				If currBlock(pos).Ticks > till Then
					pos = -1
					Return False
				End If
				hasCurrent = True
				Return True
			End Function

			Public Overridable ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If
					Return currBlock(pos)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Private blockIterator As IEnumerator(Of TimeSeriesBlock)
			Private hasCurrent As Boolean
			Private currBlock As TimeSeriesBlock
			Private pos As Integer
			Private from As Long
			Private till As Long
		End Class

		Private Class TimeSeriesReverseEnumerator
			Implements IEnumerator(Of T)
			Implements IEnumerable(Of T)
			Friend Sub New(blockIterator As IEnumerator(Of TimeSeriesBlock), from As Long, till As Long)
				Me.till = till
				Me.from = from
				Me.blockIterator = blockIterator
				Reset()
			End Sub

			Public Function GetEnumerator() As IEnumerator(Of T) Implements IEnumerable(Of T).GetEnumerator
				Return Me
			End Function

			Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
				Return GetEnumerator()
			End Function

			Public Sub Reset() Implements IEnumerator.Reset
				hasCurrent = False
				pos = -1
				blockIterator.Reset()
				While blockIterator.MoveNext()
					Dim block As TimeSeriesBlock = DirectCast(blockIterator.Current, TimeSeriesBlock)
					Dim n As Integer = block.used
					Dim l As Integer = 0, r As Integer = n
					While l < r
						Dim i As Integer = (l + r) >> 1
						If till >= block(i).Ticks Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(l = r AndAlso (l = n OrElse block(l).Ticks > till))
					If l > 0 Then
						pos = l - 1
						currBlock = block
						Return
					End If
				End While
			End Sub

			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If hasCurrent Then
					hasCurrent = False
					If System.Threading.Interlocked.Decrement(pos) < 0 Then
						If blockIterator.MoveNext() Then
							currBlock = DirectCast(blockIterator.Current, TimeSeriesBlock)
							pos = currBlock.used - 1
						Else
							pos = -1
							Return False
						End If
					End If
				ElseIf pos < 0 Then
					Return False
				End If
				If currBlock(pos).Ticks < from Then
					pos = -1
					Return False
				End If
				hasCurrent = True
				Return True
			End Function

			Public Overridable ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					If Not hasCurrent Then
						Throw New InvalidOperationException()
					End If
					Return currBlock(pos)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Private blockIterator As IEnumerator(Of TimeSeriesBlock)
			Private hasCurrent As Boolean
			Private currBlock As TimeSeriesBlock
			Private pos As Integer
			Private from As Long
			Private till As Long
		End Class

		Public Function GetEnumerator() As IEnumerator(Of T)
			Return iterator(0, Int64.MaxValue, IterationOrder.AscentOrder).GetEnumerator()
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public Function GetEnumerator(from As DateTime, till As DateTime) As IEnumerator(Of T)
			Return iterator(from.Ticks, till.Ticks, IterationOrder.AscentOrder).GetEnumerator()
		End Function

		Public Function GetEnumerator(from As DateTime, till As DateTime, order As IterationOrder) As IEnumerator(Of T)
			Return iterator(from.Ticks, till.Ticks, order).GetEnumerator()
		End Function

		Public Function GetEnumerator(order As IterationOrder) As IEnumerator(Of T)
			Return iterator(0, Int64.MaxValue, order).GetEnumerator()
		End Function

		Public Function Range(from As DateTime, till As DateTime) As IEnumerable(Of T)
			Return iterator(from.Ticks, till.Ticks, IterationOrder.AscentOrder)
		End Function

		Public Function Range(from As DateTime, till As DateTime, order As IterationOrder) As IEnumerable(Of T)
			Return iterator(from.Ticks, till.Ticks, order)
		End Function

		Public Function Range(order As IterationOrder) As IEnumerable(Of T)
			Return iterator(0, Int64.MaxValue, order)
		End Function

		Public Function Till(till__1 As DateTime) As IEnumerable(Of T)
			Return iterator(0, till__1.Ticks, IterationOrder.DescentOrder)
		End Function

		Public Function From(from__1 As DateTime) As IEnumerable(Of T)
			Return iterator(from__1.Ticks, Int64.MaxValue, IterationOrder.AscentOrder)
		End Function

		Public Function Reverse() As IEnumerable(Of T)
			Return iterator(0, Int64.MaxValue, IterationOrder.DescentOrder)
		End Function

		Private Function iterator(from As Long, till As Long, order As IterationOrder) As IEnumerable(Of T)
			Dim enumerator As IEnumerator(Of TimeSeriesBlock) = index.GetEnumerator(from - maxBlockTimeInterval, till, order)
			Return If(order = IterationOrder.AscentOrder, DirectCast(New TimeSeriesEnumerator(enumerator, from, till), IEnumerable(Of T)), DirectCast(New TimeSeriesReverseEnumerator(enumerator, from, till), IEnumerable(Of T)))
		End Function

		Public ReadOnly Property FirstTime() As DateTime
			Get
				For Each block As TimeSeriesBlock In index
					Return New DateTime(block.timestamp)
				Next
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End Get
		End Property

		Public ReadOnly Property LastTime() As DateTime
			Get
				For Each block As TimeSeriesBlock In index.Range(Nothing, Nothing, IterationOrder.DescentOrder)
					Return New DateTime(block(block.used - 1).Ticks)
				Next
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End Get
		End Property

		Public ReadOnly Property Count() As Integer
			Get
				Dim n As Integer = 0
				For Each block As TimeSeriesBlock In index
					n += block.used
				Next
				Return n
			End Get
		End Property

		Public Default ReadOnly Property Item(timestamp As DateTime) As T
			Get
				Dim time As Long = timestamp.Ticks
				For Each block As TimeSeriesBlock In index.Range(time - maxBlockTimeInterval, time, IterationOrder.AscentOrder)
					Dim n As Integer = block.used
					Dim l As Integer = 0, r As Integer = n
					While l < r
						Dim i As Integer = (l + r) >> 1
						If time > block(i).Ticks Then
							l = i + 1
						Else
							r = i
						End If
					End While
					Debug.Assert(l = r AndAlso (l = n OrElse block(l).Ticks >= time))
					If l < n AndAlso block(l).Ticks = time Then
						Return block(l)
					End If
				Next
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End Get
		End Property

		Public Function Contains(timestamp As DateTime) As Boolean
			Try
				Dim val As T = Me(timestamp)
				Return True
			Catch e As DatabaseException
				If e.Code = DatabaseException.ErrorCode.KEY_NOT_FOUND Then
					Return False
				End If
				Throw
			End Try
		End Function

		Public Function Remove(from As DateTime, till As DateTime) As Integer
			Return remove(from.Ticks, till.Ticks)
		End Function

		Public Function RemoveFrom(from As DateTime) As Integer
			Return remove(from.Ticks, Int64.MaxValue)
		End Function

		Public Function RemoveTill(till As DateTime) As Integer
			Return remove(0, till.Ticks)
		End Function

		Public Function RemoveAll() As Integer
			Return remove(0, Int64.MaxValue)
		End Function

		Private Function remove(from As Long, till As Long) As Integer
			Dim nRemoved As Integer = 0
			Dim blockIterator As IEnumerator(Of TimeSeriesBlock) = index.GetEnumerator(from - maxBlockTimeInterval, till)

			While blockIterator.MoveNext()
				Dim block As TimeSeriesBlock = DirectCast(blockIterator.Current, TimeSeriesBlock)
				Dim n As Integer = block.used
				Dim l As Integer = 0, r As Integer = n
				While l < r
					Dim i As Integer = (l + r) >> 1
					If from > block(i).Ticks Then
						l = i + 1
					Else
						r = i
					End If
				End While
				Debug.Assert(l = r AndAlso (l = n OrElse block(l).Ticks >= from))
				While r < n AndAlso block(r).Ticks <= till
					r += 1
					nRemoved += 1
				End While
				If l = 0 AndAlso r = n Then
					index.Remove(block.timestamp, block)
					block.Deallocate()
					blockIterator = index.GetEnumerator(from - maxBlockTimeInterval, till)
				ElseIf l <> r Then
					If l = 0 Then
						index.Remove(block.timestamp, block)
						block.timestamp = block(r).Ticks
						index.Put(block.timestamp, block)
						blockIterator = index.GetEnumerator(from - maxBlockTimeInterval, till)
					End If
					Array.Copy(block.Ticks, r, block.Ticks, l, n - r)
					block.used = l + n - r
					block.Modify()
				End If
			End While
			Return nRemoved
		End Function

		Private Sub addNewBlock(t As T)
			Dim block As New TimeSeriesBlock(blockSize)
			block.timestamp = t.Ticks
			block.used = 1
			block(0) = t
			index.Put(block.timestamp, block)
		End Sub

		Private Sub insertInBlock(block As TimeSeriesBlock, tick As T)
			Dim t As Long = tick.Ticks
			Dim i As Integer, n As Integer = block.used

			Dim l As Integer = 0, r As Integer = n
			While l < r
				i = (l + r) >> 1
				If t > block(i).Ticks Then
					l = i + 1
				Else
					r = i
				End If
			End While
			Debug.Assert(l = r AndAlso (l = n OrElse block(l).Ticks >= t))
			If r = 0 Then
				If block(n - 1).Ticks - t > maxBlockTimeInterval OrElse n = block.Ticks.Length Then
					addNewBlock(tick)
					Return
				End If
				block.timestamp = t
			ElseIf r = n Then
				If t - block(0).Ticks > maxBlockTimeInterval OrElse n = block.Ticks.Length Then
					addNewBlock(tick)
					Return
				End If
			End If
			If n = block.Ticks.Length Then
				addNewBlock(block(n - 1))
				Array.Copy(block.Ticks, r, block.Ticks, r + 1, n - r - 1)
			Else
				If n <> r Then
					Array.Copy(block.Ticks, r, block.Ticks, r + 1, n - r)
				End If
				block.used += 1
			End If
			block(r) = tick
			block.Modify()
		End Sub

		Friend Sub New(db As IDatabase, blockSize As Integer, maxBlockTimeInterval As Long)
			Me.blockSize = blockSize
			Me.maxBlockTimeInterval = maxBlockTimeInterval
			index = db.CreateIndex(Of Long, TimeSeriesBlock)(IndexType.Unique)
		End Sub
		Friend Sub New()
		End Sub

		Public Overrides Sub Deallocate()
			For Each block As TimeSeriesBlock In index
				block.Deallocate()
			Next
			index.Deallocate()
			MyBase.Deallocate()
		End Sub

		Private index As IIndex(Of Long, TimeSeriesBlock)
		Private maxBlockTimeInterval As Long
		Private blockSize As Integer
	End Class
End Namespace
