#If WITH_XML Then
Namespace Volante

	''' <summary> Exception thrown during import of data from XML file in database
	''' </summary>
	Public Class XmlImportException
		Inherits ApplicationException
		Public Overridable ReadOnly Property MessageText() As System.String
			Get
				Return message
			End Get
		End Property

		Public Overridable ReadOnly Property Line() As Integer
			Get
				Return m_line
			End Get
		End Property

		Public Overridable ReadOnly Property Column() As Integer
			Get
				Return m_column
			End Get
		End Property

		Public Sub New(line As Integer, column As Integer, message As [String])
			MyBase.New("In line " & line & " column " & column & ": " & message)
			Me.m_line = line
			Me.m_column = column
			Me.message = message
		End Sub

		Private m_line As Integer
		Private m_column As Integer
		Private message As [String]
	End Class
End Namespace
#End If
