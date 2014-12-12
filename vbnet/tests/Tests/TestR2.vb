Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	Public Class TestR2Result
		Inherits TestResult
	End Class

	Public Class TestR2
		Inherits Persistent
		Implements ITest
		Private Class SpatialObject
			Inherits Persistent
			Public rect As RectangleR2
		End Class

		Private index As ISpatialIndexR2(Of SpatialObject)

		Public Sub Run(config As TestConfig)
			Const  nObjectsInTree As Integer = 1000

			Dim so As SpatialObject
			Dim r As RectangleR2
			Dim count As Integer = config.Count
			Dim res = New TestR2Result()
			config.Result = res
			Dim db As IDatabase = config.GetDatabase()
			Dim root As TestR2 = DirectCast(db.Root, TestR2)
			Tests.Assert(root Is Nothing)
			root = New TestR2()
			root.index = db.CreateSpatialIndexR2(Of SpatialObject)()
			db.Root = root
			Dim index = root.index

			Dim rectangles As RectangleR2() = New RectangleR2(nObjectsInTree - 1) {}
			Dim key As Long = 1999
			For i As Integer = 0 To count - 1
				Dim j As Integer = i Mod nObjectsInTree
				If i >= nObjectsInTree Then
					r = rectangles(j)
					Dim sos As SpatialObject() = root.index.[Get](r)
					Dim po As SpatialObject = Nothing
					Dim n As Integer = 0
					For k As Integer = 0 To sos.Length - 1
						so = sos(k)
						If r.Equals(so.rect) Then
							po = so
						Else
							Tests.Assert(r.Intersects(so.rect))
						End If
					Next
					Tests.Assert(po IsNot Nothing)
					For k As Integer = 0 To nObjectsInTree - 1
						If r.Intersects(rectangles(k)) Then
							n += 1
						End If
					Next
					Tests.Assert(n = sos.Length)

					n = 0
					For Each o As SpatialObject In root.index.Overlaps(r)
						Tests.Assert(o Is sos(System.Math.Max(System.Threading.Interlocked.Increment(n),n - 1)))
					Next
					Tests.Assert(n = sos.Length)

					root.index.Remove(r, po)
					po.Deallocate()
				End If
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
				Dim top As Integer = CInt(key Mod 1000)
				Dim left As Integer = CInt(key \ 1000 Mod 1000)
				key = (3141592621L * key + 2718281829L) Mod 1000000007L
				Dim bottom As Integer = top + CInt(key Mod 100)
				Dim right As Integer = left + CInt(key \ 100 Mod 100)
				so = New SpatialObject()
				r = New RectangleR2(top, left, bottom, right)
				so.rect = r
				rectangles(j) = r
				root.index.Put(r, so)

				If i Mod 100 = 0 Then
					db.Commit()
				End If
			Next
			db.Commit()
			Dim wrappingRect As RectangleR2 = root.index.WrappingRectangle
			Dim objsTmp As SpatialObject() = root.index.[Get](wrappingRect)
			Tests.Assert(root.index.Count = objsTmp.Length)
			Dim objs = New List(Of SpatialObject)()
			objs.AddRange(objsTmp)

			For Each spo As var In root.index
				Tests.Assert(objs.Contains(spo))
			Next

			Dim de As IDictionaryEnumerator = root.index.GetDictionaryEnumerator()
			While de.MoveNext()
				Dim spo = DirectCast(de.Value, SpatialObject)
				Dim rect = DirectCast(de.Key, RectangleR2)
				Tests.Assert(spo.rect.EqualsTo(rect))
				Tests.Assert(objs.Contains(spo))
			End While

			Dim rand = New Random()
			While root.index.Count > 5
				Dim idx As Integer = rand.[Next](root.index.Count)
				Dim o As SpatialObject = objs(idx)
				If rand.[Next](10) > 5 Then
					root.index.Remove(o.rect, o)
				Else
					root.index.Remove(wrappingRect, o)
				End If
				objs.RemoveAt(idx)
			End While

			root.index.Clear()
			db.Close()
		End Sub
	End Class
End Namespace
