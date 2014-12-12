Imports Volante

Public MustInherit Class Guess
	Inherits Persistent
	Public MustOverride Property Yes() As Guess

	Public MustOverride Property No() As Guess

	Public MustOverride Property Question() As String

	Friend Shared Function Create(db As IDatabase, no As Guess, question As String, yes As Guess) As Guess
		Dim guess As Guess = DirectCast(db.CreateClass(GetType(Guess)), Guess)
		guess.Yes = yes
		guess.Question = question
		guess.No = no
		Return guess
	End Function

	Friend Shared Function input(prompt As String) As String
		While True
			Console.Write(prompt)
			Dim line As [String] = Console.ReadLine().Trim()
			If line.Length <> 0 Then
				Return line
			End If
		End While
	End Function

	Friend Shared Function askQuestion(question As System.String) As Boolean
		Dim answer As String = input(question)
		Return answer.ToUpper().Equals("Y") OrElse answer.ToUpper().Equals("YES")
	End Function

	Friend Shared Function whoIsIt(db As IDatabase, parent As Guess) As Guess
		Dim animal As System.String = input("What is it ? ")
		Dim difference As System.String = input("What is a difference from other ? ")
		Return Create(db, parent, difference, Create(db, Nothing, animal, Nothing))
	End Function

	Friend Function dialog() As Guess
		If askQuestion("May be, " & Question & " (y/n) ? ") Then
			If Yes Is Nothing Then
				Console.WriteLine("It was very simple question for me...")
			Else
				Dim clarify As Guess = Yes.dialog()
				If clarify IsNot Nothing Then
					Yes = clarify
				End If
			End If
		Else
			If No Is Nothing Then
				If Yes Is Nothing Then
					Return whoIsIt(Database, Me)
				Else
					No = whoIsIt(Database, Nothing)
				End If
			Else
				Dim clarify As Guess = No.dialog()
				If clarify IsNot Nothing Then
					No = clarify
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
				root = whoIsIt(db, Nothing)
				db.Root = root
			Else
				root.dialog()
			End If
			db.Commit()
		End While

		System.Console.WriteLine("End of the game")
		db.Close()
	End Sub
End Class
