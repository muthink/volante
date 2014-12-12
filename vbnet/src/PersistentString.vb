Namespace Volante
	''' <summary>
	''' Class encapsulating native .Net string. System.String is not a persistent object
	''' so it can not be stored in Volante as independent persistent object. 
	''' But sometimes it is needed. This class sole this problem providing implicit conversion
	''' operator from System.String to PerisstentString.
	''' Also PersistentString class is mutable (i.e. unlike System.String, its value can be changed).
	''' </summary>
	Public Class PersistentString
		Inherits PersistentResource
		Public Sub New()
			Me.str = ""
		End Sub

		''' <summary>
		''' Constructor of peristent string
		''' </summary>
		''' <param name="str">.Net string</param>
		Public Sub New(str As String)
			Me.str = str
		End Sub

		''' <summary>
		''' Get .Net string
		''' </summary>
		''' <returns>.Net string</returns>
		Public Overrides Function ToString() As String
			Return str
		End Function

		''' <summary>
		''' Append string to the current string value of PersistentString
		''' </summary>
		''' <param name="tail">appended string</param>
		Public Sub Append(tail As String)
			Modify()
			str = str & tail
		End Sub

		''' <summary>
		''' Assign new string value to the PersistentString
		''' </summary>
		''' <param name="str">new string value</param>
		Public Sub [Set](str As String)
			Modify()
			Me.str = str
		End Sub

		''' <summary>
		''' Get current string value
		''' </summary>
		''' <returns>.Net string</returns>
		Public Function [Get]() As String
			Return str
		End Function

		''' <summary>
		''' Operator for implicit convertsion from System.String to PersistentString
		''' </summary>
		''' <param name="str">.Net string</param>
		''' <returns>PersistentString</returns>
		Public Shared Widening Operator CType(str As String) As PersistentString
			Return New PersistentString(str)
		End Operator

		''' <summary>
		''' Operator for implicit convertsion from PersistentString to System.String
		''' </summary>
		''' <param name="str">PersistentString</param>
		''' <returns>.Net string</returns>
		Public Shared Widening Operator CType(str As PersistentString) As String
			Return str.ToString()
		End Operator

		Private str As String
	End Class
End Namespace
