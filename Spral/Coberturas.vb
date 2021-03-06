﻿' (C) Copyright 2015 by Pedro Toipa Coelho (toipacoelho@ua.pt)

Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.EditorInput
Imports Autodesk.AutoCAD.Geometry
Imports FileHelpers
Imports System.IO

Namespace Spral

    Public Class Coberturas

        ''Get the current document
        Dim acDoc As Document = Application.DocumentManager.MdiActiveDocument
        ''Get the current database
        Dim acCurDb As Database = acDoc.Database

        Dim perfiltype As String = Nothing
        Dim prfwidth As Double = 0.1
        Dim rpwith As Double = 0.05
        Dim NRIPAS As Double = 0
        Dim NPERFIS As Double = 0
        Private mirror As Matrix3d
        Private mflag As Boolean
        Private lastcota = 0
        Private telha As Double
        Private lista As List(Of Export)

        ''constructor
        Public Sub Coberturas(t As Double, dir As Boolean, mir As Boolean)
            acDoc.LockDocument()

            Dim hs As HostApplicationServices = HostApplicationServices.Current
            Dim fp As String = hs.FindFile(acDoc.Name, acDoc.Database, FindFileHint.Default)
            Dim fn As String = Path.GetFileNameWithoutExtension(fp)
            Dim fd As String = Path.GetDirectoryName(fp)

            lista = New List(Of Export)()
            Dim engine = New FileHelperAsyncEngine(Of Export)()

            If My.Computer.FileSystem.FileExists(fd + "\" + fn + ".csv") Then
                Using engine.BeginReadFile(fd + "\" + fn + ".csv")
                    ' The engine is IEnumerable
                    For Each cust As Export In engine
                        ' your code here
                        For flag = cust.count To 1 Step -1
                            add(cust.reference)
                        Next
                    Next
                End Using
            End If

            Dim acPoly As Polyline = getPolyline()

            telha = t

            ''referente ao vao
            Dim aPt As Point3d = promptForPoint("de ínico do segmento.")
            Dim bPt As Point3d = promptForPoint("de fim do segmento.")
            Dim rotation As Matrix3d = New Matrix3d()

            If dir = True Then
                rotation = getRotationY(aPt, bPt)
            Else
                rotation = getRotationX(aPt, bPt)
            End If

            If mir = True Then
                mirror = Matrix3d.Mirroring(New Line3d(aPt, bPt))
                acPoly = acPoly.GetTransformedCopy(mirror)
                mflag = True
            End If

            Dim poly As Polyline = acPoly.GetTransformedCopy(rotation)
            Dim startpoint As Point3d = getStartPoint(poly, aPt)
            Dim length As Double = startpoint.DistanceTo(getLastPoint(poly, aPt))

            drawCobertura(poly, length, startpoint, rotation)

            Dim msg As MsgBoxResult = MsgBox("A desenhar")
            Dim err As MsgBoxResult
            Dim Box As MsgBoxResult = MsgBox("Confimar laje?", MsgBoxStyle.YesNo)
            If Box = MsgBoxResult.Yes Then
                'escrever
                acDoc.Editor.WriteMessage(vbLf + "Exported to: " + fd + "\" + fn + ".csv")
                Using engine.BeginWriteFile(fd + "\" + fn + ".csv")
                    For Each cust As Export In lista
                        engine.WriteNext(cust)
                    Next
                End Using
            Else
                '' nao
                err = MsgBox("Exportação de referências cancelada, por favor execute Ctrl+Z")
            End If

        End Sub

        Private Sub add(v As String)
            Dim count As Integer = 1
            Dim i As Integer
            If (lista.Exists(Function(x) x.reference = v)) Then
                i = lista.FindIndex(Function(x) x.reference = v)
                count = lista(i).count + 1
                lista.RemoveAt(i)
            End If
            lista.Add(New Export With {.reference = v, .count = count})
        End Sub

        ''prompts user for point 3D
        Private Function promptForPoint(msg As String) As Point3d

            Dim pPtRes As PromptPointResult
            Dim pPtOpts As PromptPointOptions = New PromptPointOptions("")

            '' Prompt for the start point
            pPtOpts.Message = vbLf & "Marcar ponto " & msg
            pPtRes = acDoc.Editor.GetPoint(pPtOpts)
            Dim ptPoint As Point3d = pPtRes.Value

            Dim point As Point2d = New Point2d(ptPoint.X, ptPoint.Y)

            '' Exit if the user presses ESC or cancels the command
            If pPtRes.Status = PromptStatus.Cancel Then
                Return Nothing
                Exit Function
            End If

            Return ptPoint
        End Function

        ''gets polyline
        Private Function getPolyline() As Polyline

            Dim acPoly As Polyline = New Polyline()
            Dim peo As New PromptEntityOptions(vbLf & "Select a polyline")
            peo.SetRejectMessage("Please select a polyline")
            peo.AddAllowedClass(GetType(Polyline), True)
            Dim per As PromptEntityResult = acDoc.Editor.GetEntity(peo)
            Dim tr As Transaction = acCurDb.TransactionManager.StartTransaction()
            Using tr
                acPoly = TryCast(tr.GetObject(per.ObjectId, OpenMode.ForRead), Polyline)
                tr.Commit()
            End Using

            Return acPoly
        End Function

        ''gets rotation with the X axys
        Private Function getRotationX(basePoint As Point3d, cursorPoint As Point3d) As Matrix3d
            ' get the vector from basepoint to cursorPoint
            Dim direction As Vector3d = basePoint.GetVectorTo(cursorPoint)

            ' compute the angle between this vector and X axis
            Dim angle As Double = direction.GetAngleTo(Vector3d.XAxis, Vector3d.ZAxis)

            ' build a transformation matrix to rotate points
            Dim rotation As Matrix3d = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint)

            Return rotation
        End Function

        ''gets rotation with the Y axys
        Private Function getRotationY(basePoint As Point3d, cursorPoint As Point3d) As Matrix3d
            ' get the vector from basepoint to cursorPoint
            Dim direction As Vector3d = basePoint.GetVectorTo(cursorPoint)

            ' compute the angle between this vector and X axis
            Dim angle As Double = direction.GetAngleTo(Vector3d.YAxis, Vector3d.ZAxis)

            ' build a transformation matrix to rotate points
            Dim rotation As Matrix3d = Matrix3d.Rotation(angle, Vector3d.ZAxis, basePoint)

            Return rotation
        End Function

        ''gets "first" point of polyline
        Private Function getStartPoint(pline As Polyline, aPt As Point3d) As Point3d
            Dim result As Point3d = aPt
            ' iterate the vertices
            For i As Integer = 0 To pline.NumberOfVertices - 1
                Dim pt As Point3d = pline.GetPoint3dAt(i)
                ' compare the rotated point X coordinate to the result point one
                If pt.X < result.X Then
                    result = pt
                End If
            Next

            Return New Point3d(result.X, aPt.Y, 0)
        End Function

        ''gets "last" point of polyline
        Private Function getLastPoint(pline As Polyline, aPt As Point3d) As Point3d
            Dim result As Point3d = aPt
            ' iterate the vertices
            For i As Integer = 0 To pline.NumberOfVertices - 1
                Dim pt As Point3d = pline.GetPoint3dAt(i)
                ' compare the rotated point X coordinate to the result point one
                If pt.X > result.X Then
                    result = pt
                End If
            Next

            Return New Point3d(result.X, aPt.Y, 0)
        End Function

        ''função awesome que nos da a largura da laje! x wyse
        Private Function getWidth(pline As Polyline, apt As Point3d) As Double
            Dim result As Double = 0
            Dim aux As Point3d = apt
            Dim l As Line = New Line(New Point3d(apt.X, -pline.Length, 0), New Point3d(apt.X, apt.Y + 2 * pline.Length, 0))
            Dim pts As Point3dCollection = New Point3dCollection()
            Dim lista As List(Of Point3d) = New List(Of Point3d)

            pline.IntersectWith(l, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)

            For Each pt In pts
                lista.Add(pt)
            Next
            For i = 1 To lista.Count - 1
                If lista.Item(i).DistanceTo(lista.Item(i - 1)) > result Then result = lista.Item(i).DistanceTo(lista.Item(i - 1))
            Next

            Return result
        End Function

        ''auxiliar function to draw rectangular shapes
        Private Sub drawRectangle(a As Point2d, b As Point2d, c As Point2d, d As Point2d, rotation As Matrix3d)
            '' Start a transaction
            Using acTrans As Transaction = acCurDb.TransactionManager.StartTransaction()
                '' Open the Block table for read
                Dim acBlkTbl As BlockTable
                acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead)
                '' Open the Block table record Model space for write
                Dim acBlkTblRec As BlockTableRecord
                acBlkTblRec = acTrans.GetObject(acBlkTbl(BlockTableRecord.ModelSpace), OpenMode.ForWrite)

                '' Create a polyline with four segments (4 points)
                Using acPoly As Polyline = New Polyline()

                    acPoly.SetDatabaseDefaults()
                    acPoly.AddVertexAt(0, a, 0, 0, 0)
                    acPoly.AddVertexAt(1, b, 0, 0, 0)
                    acPoly.AddVertexAt(2, c, 0, 0, 0)
                    acPoly.AddVertexAt(3, d, 0, 0, 0)
                    acPoly.Closed = True

                    ''rotates polyline
                    acPoly.TransformBy(rotation.Inverse())

                    If (mflag = True) Then
                        acPoly.TransformBy(mirror)
                    End If


                    '' Add the new object to the block table record and the transaction
                    acBlkTblRec.AppendEntity(acPoly)
                    acTrans.AddNewlyCreatedDBObject(acPoly, True)
                End Using

                '' Save the new object to the database
                acTrans.Commit()
            End Using
        End Sub

        ''é preciso compor esta salganhada e documentar
        Private Sub drawCobertura(poly As Polyline, length As Double, startpoint As Point3d, rotation As Matrix3d)
            Dim meio As Double = length / 2

            Dim maxWidth As Double = getMaxWidth(poly, startpoint)
            Dim buffer As Double = getPerfil(maxWidth)
            Dim a As Point2d = New Point2d()
            Dim b As Point2d = New Point2d()
            Dim c As Point2d = New Point2d()
            Dim d As Point2d = New Point2d()
            Dim e As Point2d = New Point2d()
            Dim f As Point2d = New Point2d()
            Dim g As Point2d = New Point2d()
            Dim h As Point2d = New Point2d()
            Dim aux As Double

            Dim mf As Double = meio - buffer / 2
            Dim ml As Double = meio + buffer / 2

            Dim flag As Double

            For i = 0 To maxWidth Step telha
                e = New Point2d(startpoint.X + mf, startpoint.Y + i)
                f = New Point2d(e.X + buffer, e.Y)
                g = New Point2d(f.X, f.Y + rpwith)
                h = New Point2d(e.X, g.Y)
                If assertInPoly(e, f, g, h, poly) Then
                    If Not queromijar(poly, f, e) Then
                        drawRectangle(e, f, g, h, rotation)
                        'add(getReferenceRipa(buffer))
                        flag += e.GetDistanceTo(f)
                    Else
                        Dim pts As Point3dCollection = queroMeio(poly, e, f)
                        Dim pts2 As Point3dCollection = queroMeio(poly, g, h)
                        e = New Point2d(pts2(0).X, pts(0).Y)
                        'acDoc.Editor.WriteMessage(pts(1).ToString)
                        If (pts(1).X <> 0) Then
                            f = New Point2d(pts(1).X, pts(1).Y)
                        End If
                        g = New Point2d(f.X, f.Y + rpwith)
                        h = New Point2d(e.X, g.Y)
                        drawRectangle(e, f, g, h, rotation)
                        'add(getReferenceRipa(buffer))
                        flag += e.GetDistanceTo(f)
                        End If
                    End If
            Next

            'esquerda
            For i = mf To 0 Step buffer * (-1)

                Dim min As Double = getlowerpoint(poly, startpoint.X + i - prfwidth / 2).Y
                If min > getlowerpoint(poly, startpoint.X + i + prfwidth / 2).Y Then
                    min = getlowerpoint(poly, startpoint.X + i + prfwidth / 2).Y
                End If

                a = New Point2d(startpoint.X + i - prfwidth / 2, min)
                b = New Point2d(a.X + prfwidth, a.Y)

                Dim max = getWidth(poly, New Point3d(a.X, b.Y, 0))
                If max < getWidth(poly, New Point3d(b.X, b.Y, 0)) Then
                    max = getWidth(poly, New Point3d(b.X, b.Y, 0))
                End If

                c = New Point2d(b.X, b.Y + max)
                d = New Point2d(a.X, c.Y)
                printDimension(b, c, rotation)
                drawRectangle(a, b, c, d, rotation)
                add(getReferenceperfil(b.GetDistanceTo(c)))

                For j = 0 To maxWidth Step telha
                    e = New Point2d(a.X + prfwidth / 2, startpoint.Y + j)
                    f = New Point2d(e.X - buffer, e.Y)
                    g = New Point2d(f.X, f.Y + rpwith)
                    h = New Point2d(e.X, g.Y)
                    If assertInPoly(e, f, g, h, poly) Then
                        If Not queromijar(poly, f, e) Then
                            drawRectangle(e, f, g, h, rotation)
                            'add(getReferenceRipa(buffer))
                            flag += e.GetDistanceTo(f)
                        Else
                            If b.GetDistanceTo(c) < getWidth(poly, New Point3d(b.X - buffer, b.Y, 0)) Then
                                aux = querocagar(poly, e, f).X
                                If aux < querocagar(poly, g, h).X Then
                                    e = New Point2d(querocagar(poly, g, h).X, querocagar(poly, e, f).Y)
                                Else
                                    e = querocagar(poly, e, f)
                                End If
                                f = New Point2d(a.X - buffer + prfwidth / 2, e.Y)
                                g = New Point2d(f.X, f.Y + rpwith)
                                h = New Point2d(e.X, g.Y)
                            Else
                                If aux < querocagar(poly, g, h).X Then
                                    f = New Point2d(querocagar(poly, g, h).X, querocagar(poly, e, f).Y)
                                Else
                                    f = querocagar(poly, e, f)
                                End If
                                g = New Point2d(f.X, f.Y + rpwith)
                            End If
                                drawRectangle(e, f, g, h, rotation)
                                'add(getReferenceRipa(buffer))
                                flag += e.GetDistanceTo(f)
                            End If
                    End If
                Next
            Next

            'direita
            For i = ml To length Step buffer

                Dim min As Double = getlowerpoint(poly, startpoint.X + i + prfwidth / 2).Y
                If min > getlowerpoint(poly, startpoint.X + i - prfwidth / 2).Y Then
                    min = getlowerpoint(poly, startpoint.X + i - prfwidth / 2).Y
                End If
                a = New Point2d(startpoint.X + i - prfwidth / 2, min)
                b = New Point2d(a.X + prfwidth, a.Y)

                Dim max = getWidth(poly, New Point3d(a.X, b.Y, 0))
                If max < getWidth(poly, New Point3d(b.X, b.Y, 0)) Then
                    max = getWidth(poly, New Point3d(b.X, b.Y, 0))
                End If

                c = New Point2d(b.X, b.Y + max)
                d = New Point2d(a.X, c.Y)

                printDimension(b, c, rotation)
                drawRectangle(a, b, c, d, rotation)
                add(getReferenceperfil(b.GetDistanceTo(c)))

                For j = 0 To maxWidth Step telha
                    e = New Point2d(a.X + prfwidth / 2, startpoint.Y + j)
                    f = New Point2d(e.X + buffer, e.Y)
                    g = New Point2d(f.X, f.Y + rpwith)
                    h = New Point2d(e.X, g.Y)
                    If assertInPoly(e, f, g, h, poly) Then
                        If Not queromijar(poly, e, f) Then
                            drawRectangle(e, f, g, h, rotation)
                            'add(getReferenceRipa(buffer))
                            flag += e.GetDistanceTo(f)
                        Else
                            If b.GetDistanceTo(c) > getWidth(poly, New Point3d(b.X - buffer, b.Y, 0)) Then
                                aux = querocagar(poly, e, f).X
                                If aux > querocagar(poly, g, h).X Then
                                    e = New Point2d(querocagar(poly, g, h).X, querocagar(poly, e, f).Y)
                                Else
                                    e = querocagar(poly, e, f)
                                End If
                                f = New Point2d(a.X + buffer + 0.05, e.Y)
                                g = New Point2d(f.X, f.Y + rpwith)
                                h = New Point2d(e.X, g.Y)
                            ElseIf b.GetDistanceTo(c) < getWidth(poly, New Point3d(b.X + buffer, b.Y, 0)) Then
                                'If aux < querocagar(poly, g, h).X Then
                                '    e = New Point2d(querocagar(poly, g, h).X, querocagar(poly, e, f).Y)
                                'Else
                                e = querocagar(poly, e, f)
                                'End If
                                f = New Point2d(a.X + buffer + 0.05, e.Y)
                                g = New Point2d(f.X, f.Y + rpwith)
                                h = New Point2d(e.X, g.Y)
                            Else
                                f = querocagar(poly, e, f)
                                g = New Point2d(f.X, f.Y + rpwith)
                            End If
                            drawRectangle(e, f, g, h, rotation)
                            'add(getReferenceRipa(buffer))
                            flag += e.GetDistanceTo(f)
                        End If
                    End If
                Next
            Next

            ''acDoc.Editor.WriteMessage("linear:" & flag)
            flag = flag / buffer
            ''acDoc.Editor.WriteMessage("n:" & flag)
            flag = Math.Ceiling(flag)
            ' acDoc.Editor.WriteMessage("ceiling:" & flag)

            While flag > 0.0
                flag -= 1
                'acDoc.Editor.WriteMessage("n:" & flag)
                add(getReferenceRipa(buffer))
            End While


        End Sub

        ''mostra a tua raça o teu querer e ambiçao, nos so queremos o dinheiro na mao
        Private Function queromijar(pline As Polyline, p As Point2d, q As Point2d) As Boolean
            Dim l1 As Line = New Line(New Point3d(p.X, p.Y, 0), New Point3d(q.X, q.Y, 0))
            Dim l2 As Line
            Dim result As Boolean = False
            Dim pts As Point3dCollection = New Point3dCollection()

            For i As Integer = 0 To pline.NumberOfVertices - 2
                l2 = New Line(pline.GetPoint3dAt(i), pline.GetPoint3dAt(i + 1))
                l1.IntersectWith(l2, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)
                If pts.Count < 1 Then
                    result = False
                Else
                    result = True
                End If
            Next

            l2 = New Line(pline.GetPoint3dAt(0), pline.GetPoint3dAt(pline.NumberOfVertices - 1))
            l1.IntersectWith(l2, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)
            If pts.Count < 1 Then
                result = False
            Else
                result = True
            End If
            'acDoc.Editor.WriteMessage(result.ToString & vbLf)

            Return result
        End Function

        Private Function querocagar(pline As Polyline, p As Point2d, q As Point2d) As Point2d
            Dim l1 As Line = New Line(New Point3d(p.X, p.Y, 0), New Point3d(q.X, q.Y, 0))
            Dim l2 As Line
            Dim result As Point2d = p
            Dim pts As Point3dCollection = New Point3dCollection()

            For i As Integer = 0 To pline.NumberOfVertices - 2
                l2 = New Line(pline.GetPoint3dAt(i), pline.GetPoint3dAt(i + 1))
                l1.IntersectWith(l2, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)
                If pts.Count < 1 Then
                    ''nao faz nada
                Else
                    result = New Point2d(pts(0).X, pts(0).Y)
                    Exit For
                End If
            Next

            l2 = New Line(pline.GetPoint3dAt(0), pline.GetPoint3dAt(pline.NumberOfVertices - 1))
            l1.IntersectWith(l2, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)
            If pts.Count < 1 Then
                ''nao faz nada
            Else
                result = New Point2d(pts(0).X, pts(0).Y)
            End If

            Return result
        End Function

        Private Function queroMeio(pline As Polyline, p As Point2d, q As Point2d) As Point3dCollection
            Dim l As Line = New Line(New Point3d(p.X, p.Y, 0), New Point3d(q.X, q.Y, 0))
            Dim result As Point2d = p
            Dim pts As Point3dCollection = New Point3dCollection()

            pline.IntersectWith(l, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)

            If pts.Count < 1 Then
                ''nao faz nada
            Else
                Return pts
            End If

            Return pts

        End Function

        Private Function getReferenceRipa(buffer As Double) As String
            Return "523010" & buffer * 100
        End Function

        Private Function getReferenceperfil(size As Double) As String
            Dim xx = "XX"
            Dim Y = "Y"

            If perfiltype = "I13" Then
                Y = "1"
            ElseIf perfiltype = "I16" Then
                Y = "2"
            ElseIf perfiltype = "I18" Then
                Y = "3"
            End If

            xx = Math.Round(size, 1) * 10
            Return "5220" & Y & 0 & xx
        End Function

        'verifica se um poly esta ou tem alguma parte no interior de outro
        Private Function assertInPoly(a As Point2d, b As Point2d, c As Point2d, d As Point2d, poly As Polyline) As Boolean
            Dim result = True
            Dim acPoly As Polyline = New Polyline()
            acPoly.SetDatabaseDefaults()
            acPoly.AddVertexAt(0, a, 0, 0, 0)
            acPoly.AddVertexAt(1, b, 0, 0, 0)
            acPoly.AddVertexAt(2, c, 0, 0, 0)
            acPoly.AddVertexAt(3, d, 0, 0, 0)
            acPoly.Closed = True

            Dim pts As New Point3dCollection()
            poly.IntersectWith(acPoly, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)

            If pts.Count <= 1 And Not IsPointInside(acPoly, poly) Then
                result = False
            End If

            Return result

        End Function

        'verfica pontos que estejam dentro de um poly
        Private Function IsPointInside(p As Polyline, pline As Polyline) As Boolean
            Dim result As Boolean = False
            For i = 0 To p.NumberOfVertices - 1
                Dim tolerance__1 As Double = Tolerance.[Global].EqualPoint
                Using mpg As New MPolygon()
                    mpg.AppendLoopFromBoundary(pline, True, tolerance__1)
                    result = mpg.IsPointInsideMPolygon(New Point3d(p.GetPoint2dAt(i).X, p.GetPoint2dAt(i).Y, 0), tolerance__1).Count = 1
                End Using
                If result = True Then Exit For
            Next
            Return result
        End Function

        'O liedson resolve hardcode ftw
        Private Function getlowerpoint(pline As Polyline, x As Double) As Point3d
            Dim result As Double = 0
            Dim l As Line = New Line(New Point3d(x, -100 * pline.Length, 0), New Point3d(x, 100 * pline.Length, 0))
            Dim pts As Point3dCollection = New Point3dCollection()
            Dim lista As List(Of Point3d) = New List(Of Point3d)

            pline.IntersectWith(l, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero)

            For Each pt In pts
                lista.Add(pt)
            Next
            If lista.Item(0).Y < lista.Item(1).Y Then
                Return lista.Item(0)
            ElseIf lista.Item(0).Y > lista.Item(1).Y Then
                Return lista.Item(1)
            End If

        End Function

        ''auxiliar function to get max width of polyline
        Private Function getMaxWidth(pline As Polyline, aPt As Point3d) As Double
            Dim min As Point3d = aPt
            Dim max As Point3d = aPt

            ' iterate the vertices
            For i As Integer = 0 To pline.NumberOfVertices - 1
                Dim pt As Point3d = pline.GetPoint3dAt(i)
                ' compare the rotated point X coordinate to the result point one
                If pt.Y < min.Y Then
                    min = pt
                End If
            Next

            ' iterate the vertices
            For i As Integer = 0 To pline.NumberOfVertices - 1
                Dim pt As Point3d = pline.GetPoint3dAt(i)
                ' compare the rotated point X coordinate to the result point one
                If pt.Y > max.Y Then
                    max = pt
                End If
            Next

            Return max.Y - min.Y
        End Function

        Private Function getPerfil(maxWidth As Double) As Double
            Dim d As Double

            If maxWidth >= 1 And maxWidth <= 5 Then
                perfiltype = "I13"
                prfwidth = 0.1
                If maxWidth <= 5 And maxWidth > 4.7 Then
                    d = 1
                ElseIf maxWidth <= 4.7 And maxWidth > 4.5 Then
                    d = 1.2
                Else
                    d = 1.4
                End If
            ElseIf maxWidth > 5 And maxWidth <= 6.5 Then
                perfiltype = "I16"
                prfwidth = 0.12
                If maxWidth <= 6.5 And maxWidth > 6.1 Then
                    d = 1
                ElseIf maxWidth <= 6.1 And maxWidth > 5.8 Then
                    d = 1.2
                Else
                    d = 1.4
                End If
            ElseIf maxWidth > 6.5 And maxWidth <= 7.4 Then
                perfiltype = "I18"
                prfwidth = 0.12
                If maxWidth <= 7.4 And maxWidth > 6.9 Then
                    d = 1
                ElseIf maxWidth <= 6.9 And maxWidth > 6.5 Then
                    d = 1.2
                Else
                    d = 1.4
                End If
            End If

            Return d
        End Function

        ''Returns inputed string
        Private Function promptForString(msg As String)

            Dim pStrOpts As PromptStringOptions = New PromptStringOptions(vbLf & msg)
            pStrOpts.AllowSpaces = True
            Dim pStrRes As PromptResult = acDoc.Editor.GetString(pStrOpts)

            Return pStrRes.StringResult

        End Function

        ''função muito gira que poe as cotas com o estilo defindo pelo utilizador no autocad
        Private Sub printDimension(a As Point2d, b As Point2d, rotation As Matrix3d)
            Dim value As Double = Math.Round(a.GetDistanceTo(b), 1)

            If value <> lastcota Then
                lastcota = value

                '' Start a transaction
                Using acTrans As Transaction = acCurDb.TransactionManager.StartTransaction()
                    '' Open the Block table for read
                    Dim acBlkTbl As BlockTable
                    acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead)
                    '' Open the Block table record Model space for write
                    Dim acBlkTblRec As BlockTableRecord
                    acBlkTblRec = acTrans.GetObject(acBlkTbl(BlockTableRecord.ModelSpace), OpenMode.ForWrite)

                    Dim d As AlignedDimension = New AlignedDimension(New Point3d(a.X, a.Y, 0), New Point3d(b.X, b.Y, 0), New Point3d(a.X, a.Y, 0), value.ToString, acCurDb.DimStyleTableId)

                    ''rotates polyline
                    d.TransformBy(rotation.Inverse())

                    If (mflag = True) Then
                        d.TransformBy(mirror)
                    End If

                    '' Add the new object to the block table record and the transaction
                    acBlkTblRec.AppendEntity(d)
                    acTrans.AddNewlyCreatedDBObject(d, True)

                    '' Save the new object to the database
                    acTrans.Commit()
                End Using
            End If

        End Sub

    End Class

End Namespace

