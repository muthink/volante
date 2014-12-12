Imports System.Collections.Generic
Namespace Volante

	''' <summary>Interface for time series elements.
	''' Objects inserted into time series must implement this interface.
	''' </summary>
	Public Interface ITimeSeriesTick
		''' <summary>
		''' Get time series element timestamp. Has the same meaning as DateTime.Ticks (100 nanoseconds). 
		''' </summary>
		ReadOnly Property Ticks() As Long
	End Interface

	''' <summary>Time series is used for efficiently handling of time series data. 
	''' Time series usually contains a very large number
	''' of small elements which are usually accessed in sucessive order. 
	''' To avoid overhead of loading elements from the disk one at a time,
	''' Volante groups several elements together and stores them 
	''' as single object (block).
	''' </summary>
	Public Interface ITimeSeries(Of T As ITimeSeriesTick)
		Inherits IPersistent
		Inherits IResource
		Inherits ICollection(Of T)
		''' <summary>
		''' Get forward iterator for time series elements in the given time interval
		''' </summary>
		''' <param name="from">inclusive time of the beginning of interval</param>
		''' <param name="till">inclusive time of the ending of interval</param>
		''' <returns>forward iterator within specified range</returns>
		Function GetEnumerator(from As DateTime, till As DateTime) As IEnumerator(Of T)

		''' <summary>
		''' Get iterator for all time series elements
		''' </summary>
		''' <param name="order">direction of iteration</param>
		''' <returns>iterator in specified direction</returns>
		Function GetEnumerator(order As IterationOrder) As IEnumerator(Of T)

		''' <summary>
		''' Get forward iterator for time series elements in a given time interval
		''' </summary>
		''' <param name="from">inclusive time of the beginning  of interval</param>
		''' <param name="till">inclusive time of the ending of interval</param>
		''' <param name="order">direction of iteration</param>
		''' <returns>iterator within specified range in the specified direction</returns>
		Function GetEnumerator(from As DateTime, till As DateTime, order As IterationOrder) As IEnumerator(Of T)

		''' <summary>
		''' Get forward iterator for time series elements in a given time interval
		''' </summary>
		''' <param name="from">inclusive time of the beginning  of interval</param>
		''' <param name="till">inclusive time of the ending of interval</param>
		''' <returns>forward iterator within specified range</returns>
		Function Range(from As DateTime, till As DateTime) As IEnumerable(Of T)

		''' <summary>
		''' Get iterator through all time series elements
		''' </summary>
		''' <param name="order">direction of iteration</param>
		''' <returns>iterator in specified direction</returns>
		Function Range(order As IterationOrder) As IEnumerable(Of T)

		''' <summary>
		''' Get iterator for time series elements belonging to the specified range
		''' </summary>
		''' <param name="from">inclusive time of the beginning  of interval</param>
		''' <param name="till">inclusive time of the ending of interval</param>
		''' <param name="order">direction of iteration</param>
		''' <returns>iterator within specified range in specified direction</returns>
		Function Range(from As DateTime, till As DateTime, order As IterationOrder) As IEnumerable(Of T)

		''' <summary>
		''' Get forward iterator for time series elements with timestamp greater or equal than specified
		''' </summary>
		''' <param name="from">inclusive time of the beginning of interval</param>
		''' <returns>forward iterator</returns>
		Function From(from__1 As DateTime) As IEnumerable(Of T)

		''' <summary>
		''' Get backward iterator for time series elements with timestamp less or equal than specified
		''' </summary>
		''' <param name="till">inclusive time of the eding of interval</param>
		''' <returns>backward iterator</returns>
		Function Till(till__1 As DateTime) As IEnumerable(Of T)

		''' <summary>
		''' Get backward iterator for time series elements 
		''' </summary>
		''' <returns>backward iterator</returns>
		Function Reverse() As IEnumerable(Of T)

		''' <summary>
		''' Get timestamp of first element in time series
		''' </summary>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorClass.KEY_NOT_FOUND) if time series is empy</exception>
		ReadOnly Property FirstTime() As DateTime

		''' <summary>
		''' Get timestamp of last element in time series
		''' </summary>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorClass.KEY_NOT_FOUND) if time series is empy</exception>
		ReadOnly Property LastTime() As DateTime

		''' <summary> 
		''' Get element for a given timestamp
		''' </summary>
		''' <param name="timestamp">time series element timestamp</param>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.ErrorClass.KEY_NOT_FOUND) if no element with such timestamp exists</exception>
		Default ReadOnly Property Item(timestamp As DateTime) As T

		''' <summary>
		''' Check if data is available in time series for the specified time
		''' </summary>
		''' <param name="timestamp">time series element timestamp</param>
		''' <returns><code>true</code> if there is element in time series with such timestamp, 
		''' <code>false</code> otherwise</returns>
		Overloads Function Contains(timestamp As DateTime) As Boolean

		''' <summary>
		''' Remove time series elements belonging to the specified range
		''' </summary>
		''' <param name="from">inclusive time of the beginning  of interval</param>
		''' <param name="till">inclusive time of the ending of interval</param>
		''' <returns>number of removed elements</returns>
		Overloads Function Remove(from As DateTime, till As DateTime) As Integer

		''' <summary>
		''' Remove time series elements with timestamp greater or equal then specified
		''' </summary>
		''' <param name="from">inclusive time of the beginning of interval</param>
		''' <returns>number of removed elements</returns>
		Function RemoveFrom(from As DateTime) As Integer

		''' <summary>
		''' Remove elements with timestamp less or equal then specified
		''' </summary>
		''' <param name="till">inclusive time of the ending of interval</param>
		''' <returns>number of removed elements</returns>
		Function RemoveTill(till As DateTime) As Integer

		''' <summary>
		''' Remove all elements
		''' </summary>
		''' <returns>number of removed elements</returns>
		Function RemoveAll() As Integer
	End Interface
End Namespace
