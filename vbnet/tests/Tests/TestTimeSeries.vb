Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestTimeSeriesResult
		Inherits TestResult
		Public InsertTime As TimeSpan
		Public SearchTime1 As TimeSpan
		Public SearchTime2 As TimeSpan
		Public RemoveTime As TimeSpan
	End Class

	Public Class TestTimeSeries
		Implements ITest
		Public Structure Quote
			Implements ITimeSeriesTick
			Public timestamp As Integer
			Public low As Single
			Public high As Single
			Public open As Single
			Public close As Single
			Public volume As Integer

			Public ReadOnly Property Ticks() As Long
				Get
					Return getTicks(timestamp)
				End Get
			End Property
		End Structure

		Public Shared rand As Random

		Public Shared Function NewQuote(timestamp As Integer) As Quote
			Dim quote As New Quote()
			quote.timestamp = timestamp
			quote.open = CSng(rand.[Next](10000)) / 100
			quote.close = CSng(rand.[Next](10000)) / 100
			quote.high = Math.Max(quote.open, quote.close)
			quote.low = Math.Min(quote.open, quote.close)
			quote.volume = rand.[Next](1000)
			Return quote
		End Function

		Public Const N_ELEMS_PER_BLOCK As Integer = 100

		Private Class Stock
			Inherits Persistent
			Public name As String
			Public quotes As ITimeSeries(Of Quote)
		End Class

		Public Sub Run(config As TestConfig)
			Dim stock As Stock
			Dim i As Integer
			Dim count As Integer = config.Count
			Dim res = New TestTimeSeriesResult()
			config.Result = res

			Dim start = DateTime.Now
			Dim db As IDatabase = config.GetDatabase()

			Dim stocks As IFieldIndex(Of String, Stock) = DirectCast(db.Root, IFieldIndex(Of String, Stock))
			Tests.Assert(stocks Is Nothing)
			stocks = db.CreateFieldIndex(Of String, Stock)("name", IndexType.Unique)
			stock = New Stock()
			stock.name = "BORL"
			stock.quotes = db.CreateTimeSeries(Of Quote)(N_ELEMS_PER_BLOCK, N_ELEMS_PER_BLOCK * TICKS_PER_SECOND * 2)
			stocks.Put(stock)
			db.Root = stocks

			Tests.Assert(Not stock.quotes.IsReadOnly)
			rand = New Random(2004)
			Dim startTimeInSecs As Integer = getSeconds(start)
			Dim currTime As Integer = startTimeInSecs
			For i = 0 To count - 1
				Dim quote As Quote = NewQuote(System.Math.Max(System.Threading.Interlocked.Increment(currTime),currTime - 1))
				stock.quotes.Add(quote)
			Next
			Tests.Assert(stock.quotes.Count = count)
			db.Commit()
			Tests.Assert(stock.quotes.Count = count)
			res.InsertTime = DateTime.Now - start

			start = DateTime.Now
			rand = New Random(2004)
			start = DateTime.Now
			i = 0
			For Each quote As Quote In stock.quotes
				Tests.Assert(quote.timestamp = startTimeInSecs + i)
				Dim open As Single = CSng(rand.[Next](10000)) / 100
				Tests.Assert(quote.open = open)
				Dim close As Single = CSng(rand.[Next](10000)) / 100
				Tests.Assert(quote.close = close)
				Tests.Assert(quote.high = Math.Max(quote.open, quote.close))
				Tests.Assert(quote.low = Math.Min(quote.open, quote.close))
				Tests.Assert(quote.volume = rand.[Next](1000))
				i += 1
			Next
			Tests.Assert(i = count)

			res.SearchTime1 = DateTime.Now - start

			start = DateTime.Now
			Dim from As Long = getTicks(startTimeInSecs + count \ 2)
			Dim till As Long = getTicks(startTimeInSecs + count)
			i = 0
			For Each quote As Quote In stock.quotes.Range(New DateTime(from), New DateTime(till), IterationOrder.DescentOrder)
				Dim expectedtimestamp As Integer = startTimeInSecs + count - i - 1
				Tests.Assert(quote.timestamp = expectedtimestamp)
				i += 1
			Next
			res.SearchTime2 = DateTime.Now - start
			start = DateTime.Now

			' insert in the middle
			stock.quotes.Add(NewQuote(startTimeInSecs - count \ 2))

			Dim n As Long = stock.quotes.Remove(stock.quotes.FirstTime, stock.quotes.LastTime)
			Tests.Assert(n = count + 1)
			Tests.Assert(stock.quotes.Count = 0)
			res.RemoveTime = DateTime.Now - start

			Dim q As Quote
			Dim qFirst As Quote = NewQuote(0)
			Dim qMiddle As Quote = NewQuote(0)
			Dim qEnd As Quote = NewQuote(0)
			For i = 0 To 9
				q = NewQuote(startTimeInSecs + i)
				stock.quotes.Add(q)
				If i = 0 Then
					qFirst = q
				ElseIf i = 5 Then
					qMiddle = q
				ElseIf i = 9 Then
					qEnd = q
				End If
			Next
			Tests.Assert(stock.quotes.Contains(qFirst))
			Tests.Assert(stock.quotes.Contains(qEnd))
			Tests.Assert(stock.quotes.Contains(qMiddle))
			Tests.Assert(stock.quotes.Remove(qFirst))
			Tests.Assert(Not stock.quotes.Contains(qFirst))
			Tests.Assert(stock.quotes.Remove(qEnd))
			Tests.Assert(Not stock.quotes.Contains(qEnd))
			Tests.Assert(stock.quotes.Remove(qMiddle))
			Tests.Assert(Not stock.quotes.Contains(qMiddle))

			Dim quotes As Quote() = New Quote(9) {}
			stock.quotes.CopyTo(quotes, 0)
			stock.quotes.Clear()

			Tests.AssertDatabaseException(Function() 
			Dim tmp As Long = stock.quotes.FirstTime.Ticks

End Function, DatabaseException.ErrorCode.KEY_NOT_FOUND)
			Tests.AssertDatabaseException(Function() 
			Dim tmp As Long = stock.quotes.LastTime.Ticks

End Function, DatabaseException.ErrorCode.KEY_NOT_FOUND)

			For i = 0 To 9
				q = NewQuote(startTimeInSecs + i)
				stock.quotes.Add(q)
			Next

			Dim e As IEnumerator = stock.quotes.GetEnumerator()
			i = 0
			While e.MoveNext()
				i += 1
			End While
			Tests.Assert(i = 10)
			Tests.Assert(Not e.MoveNext())
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim o As Object = e.Current

End Function)
			e.Reset()
			Tests.Assert(e.MoveNext())

			e = stock.quotes.Reverse().GetEnumerator()
			i = 0
			While e.MoveNext()
				i += 1
			End While
			Tests.Assert(i = 10)
			Tests.Assert(Not e.MoveNext())
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim o As Object = e.Current

End Function)
			e.Reset()
			Tests.Assert(e.MoveNext())
			Dim tStart As New DateTime(getTicks(startTimeInSecs))
			Dim tMiddle As New DateTime(getTicks(startTimeInSecs + 5))
			Dim tEnd As New DateTime(getTicks(startTimeInSecs + 9))

			Dim e2 As IEnumerator(Of Quote) = stock.quotes.GetEnumerator(tStart, tMiddle)
			VerifyEnumerator(e2, tStart.Ticks, tMiddle.Ticks)
			e2 = stock.quotes.GetEnumerator(tStart, tMiddle, IterationOrder.DescentOrder)
			VerifyEnumerator(e2, tStart.Ticks, tMiddle.Ticks, IterationOrder.DescentOrder)

			e2 = stock.quotes.GetEnumerator(IterationOrder.DescentOrder)
			VerifyEnumerator(e2, tStart.Ticks, tEnd.Ticks, IterationOrder.DescentOrder)

			e2 = stock.quotes.Range(tMiddle, tEnd, IterationOrder.AscentOrder).GetEnumerator()
			VerifyEnumerator(e2, tMiddle.Ticks, tEnd.Ticks, IterationOrder.AscentOrder)

			e2 = stock.quotes.Range(IterationOrder.DescentOrder).GetEnumerator()
			VerifyEnumerator(e2, tStart.Ticks, tEnd.Ticks, IterationOrder.DescentOrder)

			e2 = stock.quotes.Till(tMiddle).GetEnumerator()
			VerifyEnumerator(e2, tStart.Ticks, tMiddle.Ticks, IterationOrder.DescentOrder)

			e2 = stock.quotes.From(tMiddle).GetEnumerator()
			VerifyEnumerator(e2, tMiddle.Ticks, tEnd.Ticks)

			e2 = stock.quotes.Reverse().GetEnumerator()
			VerifyEnumerator(e2, tStart.Ticks, tEnd.Ticks, IterationOrder.DescentOrder)

			Tests.Assert(stock.quotes.FirstTime.Ticks = tStart.Ticks)
			Tests.Assert(stock.quotes.LastTime.Ticks = tEnd.Ticks)
			For i = 0 To 9
				Dim ticks As Long = getTicks(startTimeInSecs + i)
				Dim qTmp As Quote = stock.quotes(New DateTime(ticks))
				Tests.Assert(qTmp.Ticks = ticks)
				Tests.Assert(stock.quotes.Contains(New DateTime(ticks)))
			Next
			Tests.Assert(Not stock.quotes.Contains(New DateTime(0)))

			Tests.AssertDatabaseException(Function() 
			Dim tmp As Quote = stock.quotes(New DateTime(0))

End Function, DatabaseException.ErrorCode.KEY_NOT_FOUND)

			stock.quotes.RemoveFrom(New DateTime(getTicks(startTimeInSecs + 8)))
			stock.quotes.RemoveFrom(New DateTime(getTicks(startTimeInSecs + 2)))
			stock.quotes.RemoveAll()
			db.Commit()
			stock.quotes.Deallocate()
			db.Commit()
			db.Close()
		End Sub

		Private Sub VerifyEnumerator(e As IEnumerator(Of Quote), tickStart As Long, tickEnd As Long, Optional order As IterationOrder = IterationOrder.AscentOrder)
			Dim n As Integer = 0
			Dim tickCurr As Long = 0
			If order = IterationOrder.DescentOrder Then
				tickCurr = Long.MaxValue
			End If
			While e.MoveNext()
				Dim q As Quote = e.Current
				Tests.Assert(q.Ticks >= tickStart)
				Tests.Assert(q.Ticks <= tickEnd)
				' TODO: FAILED_TEST
				If order = IterationOrder.AscentOrder Then
					Tests.Assert(q.Ticks >= tickCurr)
				Else
					Tests.Assert(q.Ticks <= tickCurr)
				End If
				tickCurr = q.Ticks
				n += 1
			End While
			Tests.Assert(Not e.MoveNext())
			Tests.AssertException(Of InvalidOperationException)(Function() 
			Dim ticks As Long = e.Current.Ticks

End Function)
			e.Reset()
			Tests.Assert(e.MoveNext())
		End Sub

		Const TICKS_PER_SECOND As Long = 10000000L

		Shared baseDate As New DateTime(1970, 1, 1)

		Private Shared Function getSeconds(dt As DateTime) As Integer
			Return CInt((dt.Ticks - baseDate.Ticks) \ TICKS_PER_SECOND)
		End Function

		Private Shared Function getTicks(seconds As Integer) As Long
			Return baseDate.Ticks + seconds * TICKS_PER_SECOND
		End Function

	End Class

End Namespace
