﻿' (C) Copyright 2015 by Pedro Toipa Coelho (toipacoelho@ua.pt)

Imports Autodesk.AutoCAD.ApplicationServices
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.EditorInput
Imports Autodesk.AutoCAD.Geometry
Imports FileHelpers
Imports System.IO

Namespace Spral

    Public Class LajeAligeirada

        ''Get the current document
        Dim acDoc As Document = Application.DocumentManager.MdiActiveDocument
        ''Get the current database
        Dim acCurDb As Database = acDoc.Database

        Private BLCWIDTH As Double = 0.25
        Private VGTWIDTH As Double = 0.12
        Private NVIGOTAS As Integer = 0
        Private NBLOCOS As Integer = 0
        Private mirror As Matrix3d
        Private mflag As Boolean
        Private diagFlag As Boolean = False
        Private maxWidth As Double = 0
        Private lastcota As Double = 0
        Private vigotaLength As Double = 0
        Private tipovigota As String = "Y"
        Private lista As List(Of Export)
        Private blclength As Double = 0
        Private blcheigth As Double = 0
        Private blcheigthC As Double = 0
        Private dc As Double = 0
        Private init As Double

        ''construtor vindo do form
        Public Sub lajeAligeirada(pavimento As String, dcontra As Double, acontra As String, dir As Boolean, mir As Boolean)
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

            If pavimento.Length = 8 Then
                tipovigota = Val(pavimento.Chars(1)) - 1
                blclength = Convert.ToDouble(pavimento.Chars(3) & pavimento.Chars(4)) / 100
                blcheigth = Convert.ToDouble(pavimento.Chars(6) & pavimento.Chars(7)) / 100
            Else
                tipovigota = Val(pavimento.Chars(2)) - 1
                blclength = Convert.ToDouble(pavimento.Chars(4) & pavimento.Chars(5)) / 100
                blcheigth = Convert.ToDouble(pavimento.Chars(7) & pavimento.Chars(8)) / 100
            End If

            'acDoc.Editor.WriteMessage("tipo: " & pavimento.Chars(1) & "convertido " & Val(pavimento.Chars(1)) - 1)

            blcheigthC = Convert.ToDouble(acontra.Chars(3) & acontra.Chars(4))
            dc = Convert.ToDouble(dcontra)

            Dim acPoly As Polyline = getPolyline()

            ''referente ao vao
            Dim aPt As Point3d = promptForPoint("de ínico do segmento.")
            Dim bPt As Point3d = promptForPoint("de fim do segmento.")

            Dim rotation As Matrix3d

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
            init = startpoint.Y
            Dim length As Double = startpoint.DistanceTo(getLastPoint(poly, aPt))

            maxWidth = getMaxWidth(poly, aPt)

            If pavimento.Chars(0) = "3" Then
                drawVigotaTripla(poly, length, startpoint, rotation)

            ElseIf pavimento.Chars(0) = "2" Then
                drawVigotaDupla(poly, length, startpoint, rotation)
            Else
                drawVigotaSimples(poly, length, startpoint, rotation)
            End If


            'Dim savePath As New Windows.Forms.SaveFileDialog
            'savePath.ShowDialog()

            'Dim form As Confirmar = New Confirmar
            'form.Show()

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

        ''gets positions
        Private Function getTrg(x As Double)
            'acDoc.Editor.WriteMessage("Width: " & width & vbLf)
            Dim width As Double = x + getVigotaExcessSize(x) * 2
            Dim ntrg As Integer = Math.Floor(width / 2)
            ''acDoc.Editor.WriteMessage("ntrg: " & ntrg & vbLf)
            Dim bfit As Integer = Math.Floor(((width - (ntrg * 0.1)) / (ntrg + 1)) / 0.25)

            If (ntrg > 1) Then
                If ((bfit + 1) * 0.25 <= 2) Then
                    bfit = bfit + 1
                End If
                Dim twidth As Double = width - (ntrg * 0.1) - ((ntrg - 1) * bfit * 0.25)
                ''acDoc.Editor.WriteMessage("Dmidle: " & ((width - (ntrg * 0.1)) / ntrg) & vbLf)
                ''acDoc.Editor.WriteMessage("Dside: " & twidth & vbLf)
                Dim sfit As Integer = Math.Floor((twidth / 2) / 0.25)
                Dim result(ntrg - 1) As Double
                result(0) = sfit
                ' acDoc.Editor.WriteMessage("1 - N.Blocos: " & result(0) & vbLf)
                For i As Integer = 1 To ntrg - 1
                    result(i) = bfit
                    ' acDoc.Editor.WriteMessage(i & "N.Blocos: " & result(i) & vbLf)
                Next
                Return result
            Else
                Dim result(1) As Double
                result(0) = bfit
                'acDoc.Editor.WriteMessage("N.Blocos: " & result(0) & vbLf)
                Return result
            End If

        End Function

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

        ''draw laje, simple mode
        Private Sub drawVigotaSimples(poly As Polyline, length As Double, pt As Point3d, rotation As Matrix3d)
            Dim lajewidth As Double = getWidth(poly, New Point3d(pt.X, pt.Y, 0))
            'Dim blclength As Double = promptForDouble("tamanho do bloco: ")
            'blcheigth = promptForDouble("altura do bloco: ")
            Dim vgtexceed As Double = getVigotaExcessSize(lajewidth)
            Dim incremento As Double = VGTWIDTH + blclength
            Dim a As Point2d
            Dim b As Point2d
            Dim c As Point2d
            Dim d As Point2d

            drawStart(poly, pt, blclength, lajewidth, vgtexceed, rotation)

            length = length - VGTWIDTH - blclength

            Dim startPoint As Point2d = New Point2d(pt.X + VGTWIDTH + blclength, pt.Y)

            For i As Double = startPoint.X To startPoint.X + length Step incremento
                ''garante que não ultrapassa o tamanho da bases
                If i + 0.1 > startPoint.X + length Then
                    Exit For
                End If

                If i - (startPoint.X + length) <= dc And dc <> 0 Then blcheigth = blcheigthC

                lajewidth = getWidth(poly, New Point3d(i, startPoint.Y, 0))
                If getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0)) > lajewidth Then
                    lajewidth = getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0))
                End If

                'acDoc.Editor.WriteMessage(Math.Floor(lajewidth * 10 + 3))

                vgtexceed = getVigotaExcessSize(lajewidth)

                'acDoc.Editor.WriteMessage(vgtexceed)
                a = New Point2d(i, getlowerpoint(poly, i).Y - vgtexceed)
                b = New Point2d(i + VGTWIDTH, a.Y)
                c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                d = New Point2d(a.X, c.Y)
                printDimension(b, c, rotation)
                drawRectangle(a, b, c, d, rotation)
                add(getReferenceVigota(b.GetDistanceTo(c)))

                If (startPoint.X + length) - i + 0.12 < 0.2 Then
                    lajewidth = getWidth(poly, New Point3d(i, startPoint.Y, 0))
                    If getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0)) > lajewidth Then
                        lajewidth = getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0))
                    End If
                    vgtexceed = getVigotaExcessSize(lajewidth)
                    a = New Point2d(i, getlowerpoint(poly, i).Y - vgtexceed)
                    b = New Point2d(i + VGTWIDTH, a.Y)
                    c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                    d = New Point2d(a.X, c.Y)
                    printDimension(b, c, rotation)
                    drawRectangle(a, b, c, d, rotation)
                    add(getReferenceVigota(b.GetDistanceTo(c)))
                End If

                ''garante que não ultrapassa o tamanho da base
                If i + 0.12 + blclength > startPoint.X + length And (startPoint.X + length) - (i + 0.12) < blclength * 0.5 Then
                    Exit For
                End If

                lajewidth = Math.Round(getWidth(poly, New Point3d(i + 0.12 + blclength / 2, a.Y + vgtexceed, 0)), 1)
                drawBlcTrg(New Point2d(a.X, getlowerpoint(poly, b.X + blclength * 0.5).Y), blclength, lajewidth, poly, i, rotation)

            Next
        End Sub

        ''draw laje, dupla mode
        Private Sub drawVigotaDupla(poly As Polyline, length As Double, pt As Point3d, rotation As Matrix3d)
            Dim lajewidth As Double = getWidth(poly, New Point3d(pt.X, pt.Y, 0))
            'Dim blclength As Double = promptForDouble("tamanho do bloco: ")
            'blcheigth = promptForDouble("altura do bloco: ")
            Dim vgtexceed As Double = getVigotaExcessSize(lajewidth)
            Dim a As Point2d
            Dim b As Point2d
            Dim c As Point2d
            Dim d As Point2d

            Dim incremento As Double = VGTWIDTH * 2 + blclength

            drawStart(poly, pt, blclength, lajewidth, vgtexceed, rotation)

            length = length - VGTWIDTH - blclength
            Dim startPoint As Point2d = New Point2d(pt.X + VGTWIDTH + blclength, pt.Y)

            For i As Double = startPoint.X To startPoint.X + length + blclength Step incremento
                Try
                    ''garante que não ultrapassa o tamanho da base
                    If i + 0.24 > startPoint.X + length Then
                        lajewidth = getWidth(poly, New Point3d(i, startPoint.Y, 0))
                        vgtexceed = getVigotaExcessSize(lajewidth)
                        a = New Point2d(i, getlowerpoint(poly, i).Y - vgtexceed)
                        b = New Point2d(i + VGTWIDTH, a.Y)
                        c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                        d = New Point2d(a.X, c.Y)
                        printDimension(b, c, rotation)
                        drawRectangle(a, b, c, d, rotation)
                        add(getReferenceVigota(b.GetDistanceTo(c)))
                        Exit For
                    End If

                    If i - (startPoint.X + length) <= dc And dc <> 0 Then blcheigth = blcheigthC

                    lajewidth = getWidth(poly, New Point3d(i, startPoint.Y, 0))
                    If getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0)) > lajewidth Then
                        lajewidth = getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0))
                    End If
                    vgtexceed = getVigotaExcessSize(lajewidth)
                    a = New Point2d(i, getlowerpoint(poly, i).Y - vgtexceed)
                    b = New Point2d(i + VGTWIDTH, a.Y)
                    c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                    d = New Point2d(a.X, c.Y)
                    printDimension(b, c, rotation)
                    drawRectangle(a, b, c, d, rotation)
                    add(getReferenceVigota(b.GetDistanceTo(c)))

                    lajewidth = getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0))
                    If getWidth(poly, New Point3d(i + 0.24, startPoint.Y, 0)) > lajewidth Then
                        lajewidth = getWidth(poly, New Point3d(i + 0.24, startPoint.Y, 0))
                    End If
                    vgtexceed = getVigotaExcessSize(lajewidth)
                    a = New Point2d(i + VGTWIDTH, getlowerpoint(poly, i + 0.12).Y - vgtexceed)
                    b = New Point2d(i + 2 * VGTWIDTH, a.Y)
                    c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                    d = New Point2d(a.X, c.Y)
                    printDimension(b, c, rotation)
                    drawRectangle(a, b, c, d, rotation)
                    add(getReferenceVigota(b.GetDistanceTo(c)))

                    ''garante que não ultrapassa o tamanho da base
                    'If i + 0.24 + blclength > startPoint.X + length And (startPoint.X + length) - (i + 0.24) < blclength * 1.5 Then
                    '    Exit For
                    'End If

                    'lajewidth = getWidth(poly, New Point3d(b.X, a.Y + vgtexceed, 0))
                    'drawBlcTrg(New Point2d(a.X, getlowerpoint(poly, b.X).Y), blclength, lajewidth, poly, i + 0.12, rotation)

                    lajewidth = getWidth(poly, New Point3d(a.X + blclength * 0.5, a.Y + vgtexceed, 0))
                    drawBlcTrg(New Point2d(a.X, getlowerpoint(poly, a.X + blclength * 0.5).Y), blclength, lajewidth, poly, i + 0.12, rotation)
                Catch ex As Exception
                    ''i - 0.12 - blclength funciona nao sei porque, antes estava i+ 0.12
                    lajewidth = getWidth(poly, New Point3d(b.X, a.Y + vgtexceed, 0))
                    drawBlcTrg(New Point2d(a.X, getlowerpoint(poly, b.X).Y), blclength, lajewidth, poly, i - 0.12 - blclength, rotation)
                End Try


            Next
        End Sub

        ''draw laje, triple mode
        Private Sub drawVigotaTripla(poly As Polyline, length As Double, pt As Point3d, rotation As Matrix3d)
            Dim lajewidth As Double = getWidth(poly, New Point3d(pt.X, pt.Y, 0))
            'Dim blclength As Double = promptForDouble("tamanho do bloco: ")
            'Dim blcheigth = promptForDouble("altura do bloco: ")
            Dim vgtexceed As Double = getVigotaExcessSize(lajewidth)
            Dim a As Point2d
            Dim b As Point2d
            Dim c As Point2d
            Dim d As Point2d
            Dim incremento As Double = VGTWIDTH * 3 + blclength

            drawStart(poly, pt, blclength, lajewidth, vgtexceed, rotation)

            length = length - VGTWIDTH - blclength
            Dim startPoint As Point2d = New Point2d(pt.X + VGTWIDTH + blclength, pt.Y)

            For i As Double = startPoint.X To startPoint.X + length + blclength Step incremento

                ''garante que não ultrapassa o tamanho da base
                If i + 0.36 > startPoint.X + length Then
                    Exit For
                End If

                If i - (startPoint.X + length) <= dc And dc <> 0 Then blcheigth = blcheigthC

                lajewidth = getWidth(poly, New Point3d(i, startPoint.Y, 0))
                If getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0)) > lajewidth Then
                    lajewidth = getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0))
                End If
                vgtexceed = getVigotaExcessSize(lajewidth)
                a = New Point2d(i, getlowerpoint(poly, i).Y - vgtexceed)
                b = New Point2d(i + VGTWIDTH, a.Y)
                c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                d = New Point2d(a.X, c.Y)
                printDimension(b, c, rotation)
                drawRectangle(a, b, c, d, rotation)
                add(getReferenceVigota(b.GetDistanceTo(c)))


                lajewidth = getWidth(poly, New Point3d(i + 0.12, startPoint.Y, 0))
                If getWidth(poly, New Point3d(i + 0.24, startPoint.Y, 0)) > lajewidth Then
                    lajewidth = getWidth(poly, New Point3d(i + 0.24, startPoint.Y, 0))
                End If
                vgtexceed = getVigotaExcessSize(lajewidth)
                a = New Point2d(i + VGTWIDTH, getlowerpoint(poly, i + 0.12).Y - vgtexceed)
                b = New Point2d(i + 2 * VGTWIDTH, a.Y)
                c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                d = New Point2d(a.X, c.Y)
                printDimension(b, c, rotation)
                drawRectangle(a, b, c, d, rotation)
                add(getReferenceVigota(b.GetDistanceTo(c)))

                ''isto esta tudo muito feio
                lajewidth = getWidth(poly, New Point3d(i + 0.24, startPoint.Y, 0))
                If getWidth(poly, New Point3d(i + 0.36, startPoint.Y, 0)) > lajewidth Then
                    lajewidth = getWidth(poly, New Point3d(i + 0.36, startPoint.Y, 0))
                End If
                vgtexceed = getVigotaExcessSize(lajewidth)
                a = New Point2d(i + 2 * VGTWIDTH, getlowerpoint(poly, i + 0.24).Y - vgtexceed)
                b = New Point2d(i + 3 * VGTWIDTH, a.Y)
                c = New Point2d(b.X, a.Y + lajewidth + 2 * vgtexceed)
                d = New Point2d(a.X, c.Y)
                printDimension(b, c, rotation)
                drawRectangle(a, b, c, d, rotation)
                add(getReferenceVigota(b.GetDistanceTo(c)))

                'acDoc.Editor.WriteMessage((startPoint.X + length) - (i + 0.36) & "<" & blclength * 0.5 & vbLf)

                ''garante que não ultrapassa o tamanho da base
                If i + 0.36 + blclength > startPoint.X + length And (startPoint.X + length) - (i + 0.36) < blclength * 0.5 Then
                    Exit For
                End If

                lajewidth = getWidth(poly, New Point3d(i + 0.36 + blclength, a.Y + vgtexceed, 0))
                drawBlcTrg(New Point2d(a.X, getlowerpoint(poly, b.X + blclength * 0.5).Y), blclength, lajewidth, poly, i + 0.24, rotation)

            Next
        End Sub

        ''draw blocos, with tarufo if needed
        Private Function drawBlcTrg(point As Point2d, blclength As Double, width As Double, poly As Polyline, i As Double, rotation As Matrix3d) As Double
            Dim flag As Integer = 0

            'Declare a single-dimension array and set array element values
            Dim spacing = getTrg(maxWidth)

            Dim z As Integer = 0

            For j As Double = init To point.Y + width + BLCWIDTH Step BLCWIDTH

                ' teste para garantir que não ultrpassa o limite da laje
                If j + BLCWIDTH >= point.Y + width + 0.1 Then
                    Exit For
                End If

                If j >= point.Y - 0.1 Then
                    drawRectangle(New Point2d(i + VGTWIDTH, j), New Point2d(i + VGTWIDTH + blclength, j), New Point2d(i + VGTWIDTH + blclength, j + BLCWIDTH), New Point2d(i + VGTWIDTH, j + BLCWIDTH), rotation)
                    add(getReferenceBloco(blclength))

                End If
                flag += 1

                If z < spacing.Length Then
                    ' If width < 2 Then
                    If flag = spacing(z) Then
                        j += 0.1
                        z += 1
                        flag = 0
                    End If
                End If
            Next
        End Function

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

        Private Function getlowerpoint(pline As Polyline, x As Double) As Point3d
            Dim result As Double = 0
            Dim l As Line = New Line(New Point3d(x, -1000 * pline.Length, 0), New Point3d(x, 1000 * pline.Length, 0))
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

                acTrans.TransactionManager.QueueForGraphicsFlush()

                '' Save the new object to the database
                acTrans.Commit()
            End Using
        End Sub

        ''draws first iteration
        Private Sub drawStart(poly As Polyline, starpoint As Point3d, blclength As Double, width As Double, vgtleft As Double, rotation As Matrix3d)
            Dim a As Point2d = New Point2d(starpoint.X, starpoint.Y - vgtleft)
            Dim b As Point2d = New Point2d(starpoint.X + VGTWIDTH, starpoint.Y - vgtleft)
            Dim c As Point2d = New Point2d(starpoint.X + VGTWIDTH, starpoint.Y + width + vgtleft)
            Dim d As Point2d = New Point2d(starpoint.X, starpoint.Y + width + vgtleft)

            drawRectangle(a, b, c, d, rotation)
            add(getReferenceVigota(b.GetDistanceTo(c)))
            printDimension(b, c, rotation)
            drawBlcTrg(New Point2d(starpoint.X, starpoint.Y), blclength, width, poly, starpoint.X, rotation)
        End Sub

        ''calcula o tamanho da vigota, retorna o excesso
        Private Function getVigotaExcessSize(lajeWidth As Double) As Double
            Dim limit As Integer = Math.Floor(lajeWidth * 10 + 3)
            For i As Integer = 0 To limit
                vigotaLength = i
            Next
            Return (vigotaLength / 10 - lajeWidth) / 2

        End Function

        ''função awesome que nos da a largura da laje!
        Private Function getWidth(pline As Polyline, apt As Point3d) As Double
            Dim result As Double = 0
            Dim aux As Point3d = apt
            Dim l As Line = New Line(New Point3d(apt.X, 1000 * -pline.Length, 0), New Point3d(apt.X, apt.Y + 1000 * pline.Length, 0))
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

        Private Function getReferenceVigota(size As Double) As String
            Dim xx = "XX"
            xx = Math.Round(size, 1) * 10
            Return "5210" & tipovigota & 0 & xx
        End Function

        Private Function getReferenceBloco(size As Double) As String
            Dim altura As Double = blcheigth

            Dim YY As String = "YY"
            If size = 0.48 Then
                If altura = 0.09 Then YY = "01"
                If altura = 0.12 Then YY = "04"
                If altura = 0.16 Then YY = "11"
                If altura = 0.2 Then YY = "10"
            ElseIf size = 0.4 Then
                If altura = 0.09 Then YY = "01"
                If altura = 0.12 Then YY = "07"
                If altura = 0.16 Then YY = "11"
                If altura = 0.2 Then YY = "09"
                If altura = 0.25 Then YY = "10"
            ElseIf size = 0.33 Then
                If altura = 0.12 Then YY = "02"
                If altura = 0.16 Then YY = "04"
                If altura = 0.2 Then YY = "05"
                If altura = 0.25 Then YY = "06"
            ElseIf size = 0.22 Then
                If altura = 0.2 Then YY = "02"
                If altura = 0.3 Then YY = "03"
                If altura = 0.25 Then YY = "04"
            End If
            Return "5111" & size * 100 & YY
        End Function

    End Class

End Namespace

