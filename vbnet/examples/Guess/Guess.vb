Imports Volante

Public Class Guess
	Inherits Persistent
	Public yes As Guess
	Public no As Guess
	Public question As String

	Public Sub New(no As Guess, question As String, yes As Guess)
		Me.yes = yes
		Me.question = question
		Me.no = no
	End Sub

	Friend Sub New()
	End Sub

	Friend Shared Function input(prompt As String) As String
		While True
			Console.Write(prompt)
			Dim line As String = Console.ReadLine().Trim()
			If line.Length <> 0 Then
				Return line
			End If
		End While
	End Function

	Friend Shared Function askQuestion(question As String) As Boolean
		Dim answer As String = input(question)
		Return answer.ToUpper().Equals("y".ToUpper()) OrElse answer.ToUpper().Equals("yes".ToUpper())
	End Function

	Friend Shared Function whoIsIt(parent As Guess) As Guess
		Dim animal As String = input("What is it ? ")
		Dim difference As String = input("What is a difference from other ? ")
		Return New Guess(parent, difference, New Guess(Nothing, animal, Nothing))
	End Function

	Friend Function dialog() As Guess
		If askQuestion("May be, " & question & " (y/n) ? ") Then
			If yes Is Nothing Then
				Console.WriteLine("It was very simple question for me...")
			Else
				Dim clarify As Guess = yes.dialog()
				If clarify IsNot Nothing Then
					yes = clarify
					Store()
				End If
			End If
		Else
			If no Is Nothing Then
				If yes Is Nothing Then
					Return whoIsIt(Me)
				Else
					no = whoIsIt(Nothing)
					Store()
				End If
			Else
				Dim clarify As Guess = no.dialog()
				If clarify IsNot Nothing Then
					no = clarify
					Store()
				End If
			End If
		End If
		Return Nothing
	End Function

	Public Shared Sub Main(args As String())
		Dim db As IDatabase = DatabaseFactory.CreateDatabase()

		Dim dbFile As New Rc4File("guess.db", "GUESS")
		db.Open(dbFile, 4 * 1024 * 1024)
		Dim root As Guess = DirectCast(db.Root, Guess)

		While askQuestion("Think of an animal. Ready (y/n) ? ")
			If root Is Nothing Then
				root = whoIsIt(Nothing)
				db.Root = root
			Else
				root.dialog()
			End If
			db.Commit()
		End While

		Console.WriteLine("End of the game")
		db.Close()
	End Sub
End Class
