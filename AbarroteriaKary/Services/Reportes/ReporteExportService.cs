using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;                           // ← para .First() y helpers
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using AbarroteriaKary.ModelsPartial;

namespace AbarroteriaKary.Services.Reportes
{
    public class ReporteExportService : IReporteExportService
    {
        /* =================== PALETA (CAMBIAR AQUÍ) =================== */
        // Excel / Word usan HEX. En OpenXML el color va SIN '#'.
        private const string HEADER_BG = "#95b5c0";   // ← Encabezado de tabla (fuerte)
        private const string HEADER_INK = "#0b1f27";  // ← Tinta del encabezado
        private const string ZEBRA_BG = "#eaf2f5";   // ← Zebra filas pares (muy suave)

        private const string STATE_GREEN = "#0f5132"; // ← Texto Activo
        private const string STATE_RED = "#b02a37"; // ← Texto Inactivo

        // Versiones sin '#', requeridas por OpenXML (Word)
        private const string HEADER_BG_NOHASH = "95b5c0";
        private const string HEADER_INK_NOHASH = "0b1f27";
        private const string STATE_GREEN_NOHASH = "0f5132";
        private const string STATE_RED_NOHASH = "b02a37";

        /* =================== EXCEL (ClosedXML) =================== */
        public byte[] GenerarExcelAreas(IEnumerable<AreasViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Áreas");

            // Tipografía base de la hoja
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // 1) Encabezados
            string[] heads = { "Código", "Área", "Descripcion", "Fecha creación", "Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            // 1.1 Estilo del encabezado (color fuerte + títulos centrados)
            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; /* aquí centramos títulos */
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);        /* color del encabezado */
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);        /* color de texto del encabezado */

            // 2) Filas de datos
            int r = 2;
            foreach (var x in datos)
            {
                ws.Cell(r, 1).Value = x.areaId;
                ws.Cell(r, 2).Value = x.areaNombre;
                ws.Cell(r, 3).Value = x.areaDescripcion;
                ws.Cell(r, 4).Value = x.FechaCreacion;
                ws.Cell(r, 4).Style.DateFormat.Format = "dd/MM/yyyy";               /* formato de fecha */

                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 5);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;               /* sin fondo */
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)                                  /* texto verde */
                    : XLColor.FromHtml(STATE_RED);                                   /* texto rojo */

                // Zebra suave (pares)
                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // 3) Bordes y anchos
            var full = ws.Range(1, 1, r - 1, heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;             /* borde externo */
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;             /* rejilla interna muy fina */

            ws.Columns().AdjustToContents();                                        /* ajusta ancho de columnas */

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== WORD (Open XML SDK) =================== */
        public byte[] GenerarWordAreas(IEnumerable<AreasViewModel> datos)
        {
            using var ms = new MemoryStream();
            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = word.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                /* 1) Título */
                var titleP = new Paragraph(new Run(new Text("Reporte de Áreas")));
                titleP.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "200" }                         /* espacio inferior */
                );
                titleP.GetFirstChild<Run>()!.RunProperties = new RunProperties(
                    new Bold(),
                    new Color { Val = HEADER_INK_NOHASH },                             /* color del título si quieres */
                    new FontSize { Val = "28" }                                       /* 14 pt (28 = 14*2) */
                );
                body.Append(titleP);

                /* 2) Tabla */
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableStyle { Val = "TableGrid" },                              /* cuadrícula básica */
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
                )));

                /* 2.1) Encabezados con sombreado */
                string[] heads = { "Código", "Área", "Descripcion",  "Fecha creación", "Estado" };
                var headerRow = new TableRow();

                foreach (var h in heads)
                {
                    // Fondo del header (HEADER_BG) + texto en HEADER_INK + negrita
                    var cellProps = new TableCellProperties(
                        new Shading { Fill = HEADER_BG_NOHASH, Val = ShadingPatternValues.Clear, Color = "auto" }
                    );
                    var runProps = new RunProperties(new Bold(), new Color { Val = HEADER_INK_NOHASH });

                    var p = new Paragraph(new Run(new Text(h)));
                    p.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }); /* títulos centrados */
                    p.GetFirstChild<Run>()!.RunProperties = runProps;

                    headerRow.Append(new TableCell(cellProps, p));
                }
                table.Append(headerRow);

                /* 2.2) Filas */
                foreach (var x in datos)
                {
                    var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                    var estadoCap = Capitalizar(estadoRaw);                            // “Activo / Inactivo”

                    // Celdas normales
                    var c1 = new TableCell(new Paragraph(new Run(new Text(x.areaId ?? ""))));
                    var c2 = new TableCell(new Paragraph(new Run(new Text(x.areaNombre ?? ""))));
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.areaDescripcion ?? ""))));
                    var c4 = new TableCell(new Paragraph(new Run(new Text(x.FechaCreacion.ToString("dd/MM/yyyy")))));

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c5 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4,c5));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
        }




        public byte[] GenerarExcelRoles(IEnumerable<RolViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Áreas");

            // Tipografía base de la hoja
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // 1) Encabezados
            string[] heads = { "Código", "Área", "Descripcion", "Fecha creación", "Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            // 1.1 Estilo del encabezado (color fuerte + títulos centrados)
            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; /* aquí centramos títulos */
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);        /* color del encabezado */
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);        /* color de texto del encabezado */

            // 2) Filas de datos
            int r = 2;
            foreach (var x in datos)
            {
                ws.Cell(r, 1).Value = x.IdRol;
                ws.Cell(r, 2).Value = x.NombreRol;
                ws.Cell(r, 3).Value = x.DescripcionRol;
                ws.Cell(r, 4).Value = x.FechaCreacion;
                ws.Cell(r, 4).Style.DateFormat.Format = "dd/MM/yyyy";               /* formato de fecha */

                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 5);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;               /* sin fondo */
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)                                  /* texto verde */
                    : XLColor.FromHtml(STATE_RED);                                   /* texto rojo */

                // Zebra suave (pares)
                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // 3) Bordes y anchos
            var full = ws.Range(1, 1, r - 1, heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;             /* borde externo */
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;             /* rejilla interna muy fina */

            ws.Columns().AdjustToContents();                                        /* ajusta ancho de columnas */

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== WORD (Open XML SDK) =================== */
        public byte[] GenerarWordRoles(IEnumerable<RolViewModel> datos)
        {
            using var ms = new MemoryStream();
            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = word.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                /* 1) Título */
                var titleP = new Paragraph(new Run(new Text("Reporte de Áreas")));
                titleP.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "200" }                         /* espacio inferior */
                );
                titleP.GetFirstChild<Run>()!.RunProperties = new RunProperties(
                    new Bold(),
                    new Color { Val = HEADER_INK_NOHASH },                             /* color del título si quieres */
                    new FontSize { Val = "28" }                                       /* 14 pt (28 = 14*2) */
                );
                body.Append(titleP);

                /* 2) Tabla */
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableStyle { Val = "TableGrid" },                              /* cuadrícula básica */
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
                )));

                /* 2.1) Encabezados con sombreado */
                string[] heads = { "Código", "Área", "Descripcion", "Fecha creación", "Estado" };
                var headerRow = new TableRow();

                foreach (var h in heads)
                {
                    // Fondo del header (HEADER_BG) + texto en HEADER_INK + negrita
                    var cellProps = new TableCellProperties(
                        new Shading { Fill = HEADER_BG_NOHASH, Val = ShadingPatternValues.Clear, Color = "auto" }
                    );
                    var runProps = new RunProperties(new Bold(), new Color { Val = HEADER_INK_NOHASH });

                    var p = new Paragraph(new Run(new Text(h)));
                    p.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }); /* títulos centrados */
                    p.GetFirstChild<Run>()!.RunProperties = runProps;

                    headerRow.Append(new TableCell(cellProps, p));
                }
                table.Append(headerRow);

                /* 2.2) Filas */
                foreach (var x in datos)
                {
                    var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                    var estadoCap = Capitalizar(estadoRaw);                            // “Activo / Inactivo”

                    // Celdas normales
                    var c1 = new TableCell(new Paragraph(new Run(new Text(x.IdRol ?? ""))));
                    var c2 = new TableCell(new Paragraph(new Run(new Text(x.NombreRol ?? ""))));
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.DescripcionRol ?? ""))));
                    var c4 = new TableCell(new Paragraph(new Run(new Text($"{x.FechaCreacion:dd/MM/yyyy}")))); //Cambie el tipo de dato

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c5 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4, c5));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
        }


        //--------------------Usuarios---------------------------------------------

        public byte[] GenerarExcelUsuarios(IEnumerable<UsuarioListItemViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Áreas");

            // Tipografía base de la hoja
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // 1) Encabezados
            string[] heads = { "Código", "Usuario", "Empleado", "Rol", "Fecha", "Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            // 1.1 Estilo del encabezado (color fuerte + títulos centrados)
            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; /* aquí centramos títulos */
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);        /* color del encabezado */
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);        /* color de texto del encabezado */

            // 2) Filas de datos
            int r = 2;
            foreach (var x in datos)
            {
                ws.Cell(r, 1).Value = x.UsuarioId;
                ws.Cell(r, 2).Value = x.NombreUsuario;
                ws.Cell(r, 3).Value = x.EmpleadoNombre;
                ws.Cell(r, 4).Value = x.RolNombre;
                ws.Cell(r, 5).Value = x.FechaCreacion;
                ws.Cell(r, 5).Style.DateFormat.Format = "dd/MM/yyyy";               /* formato de fecha */


                 //< th > @Html.DisplayNameFor(m => m.Items[0].UsuarioId) </ th >
                 //   < th > @Html.DisplayNameFor(m => m.Items[0].NombreUsuario) </ th >
                 //   < th > @Html.DisplayNameFor(m => m.Items[0].EmpleadoNombre) </ th >
                 //   < th > @Html.DisplayNameFor(m => m.Items[0].RolNombre) </ th >
                 //   < th > @Html.DisplayNameFor(m => m.Items[0].FechaCreacion) </ th >
                 //   < th > @Html.DisplayNameFor(m => m.Items[0].ESTADO) </ th >





                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 6);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;               /* sin fondo */
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)                                  /* texto verde */
                    : XLColor.FromHtml(STATE_RED);                                   /* texto rojo */

                // Zebra suave (pares)
                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // 3) Bordes y anchos
            var full = ws.Range(1, 1, r - 1, heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;             /* borde externo */
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;             /* rejilla interna muy fina */

            ws.Columns().AdjustToContents();                                        /* ajusta ancho de columnas */

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== WORD (Open XML SDK) =================== */
        public byte[] GenerarWordUsuarios(IEnumerable<UsuarioListItemViewModel> datos)
        {
            using var ms = new MemoryStream();
            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = word.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                /* 1) Título */
                var titleP = new Paragraph(new Run(new Text("Reporte de Usuarios")));
                titleP.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "200" }                         /* espacio inferior */
                );
                titleP.GetFirstChild<Run>()!.RunProperties = new RunProperties(
                    new Bold(),
                    new Color { Val = HEADER_INK_NOHASH },                             /* color del título si quieres */
                    new FontSize { Val = "28" }                                       /* 14 pt (28 = 14*2) */
                );
                body.Append(titleP);

                /* 2) Tabla */
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableStyle { Val = "TableGrid" },                              /* cuadrícula básica */
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
                )));

                /* 2.1) Encabezados con sombreado */
                string[] heads = { "Código", "Usuario", "Empleado", "Rol", "Fecha", "Estado" };
                var headerRow = new TableRow();

                foreach (var h in heads)
                {
                    // Fondo del header (HEADER_BG) + texto en HEADER_INK + negrita
                    var cellProps = new TableCellProperties(
                        new Shading { Fill = HEADER_BG_NOHASH, Val = ShadingPatternValues.Clear, Color = "auto" }
                    );
                    var runProps = new RunProperties(new Bold(), new Color { Val = HEADER_INK_NOHASH });

                    var p = new Paragraph(new Run(new Text(h)));
                    p.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }); /* títulos centrados */
                    p.GetFirstChild<Run>()!.RunProperties = runProps;

                    headerRow.Append(new TableCell(cellProps, p));
                }
                table.Append(headerRow);

                /* 2.2) Filas */
                foreach (var x in datos)
                {
                    var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                    var estadoCap = Capitalizar(estadoRaw);                            // “Activo / Inactivo”

                    // Celdas normales
                    var c1 = new TableCell(new Paragraph(new Run(new Text(x.UsuarioId ?? ""))));
                    var c2 = new TableCell(new Paragraph(new Run(new Text(x.NombreUsuario ?? ""))));
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.EmpleadoNombre ?? ""))));
                    var c4 = new TableCell(new Paragraph(new Run(new Text(x.RolNombre ?? ""))));
                    var c5 = new TableCell(new Paragraph(new Run(new Text($"{x.FechaCreacion:dd/MM/yyyy}")))); //Cambie el tipo de dato

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c6 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4, c5,c6));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
        }





        public byte[] GenerarExcelPuestos(IEnumerable<PuestoViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Puestos");

            // Tipografía base de la hoja
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // 1) Encabezados
            string[] heads = { "Código", "Puesto", "Descripcion", "Area", "Fecha creación", "Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            // 1.1 Estilo del encabezado (color fuerte + títulos centrados)
            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; /* aquí centramos títulos */
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);        /* color del encabezado */
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);        /* color de texto del encabezado */

            // 2) Filas de datos
            int r = 2;
            foreach (var x in datos)
            {
                ws.Cell(r, 1).Value = x.PUESTO_ID;
                ws.Cell(r, 2).Value = x.PUESTO_NOMBRE;
                ws.Cell(r, 3).Value = x.PUESTO_DESCRIPCION;
                ws.Cell(r, 4).Value = x.AREA_NOMBRE;
                ws.Cell(r, 5).Value = x.FECHA_CREACION;
                ws.Cell(r, 5).Style.DateFormat.Format = "dd/MM/yyyy";               /* formato de fecha */

                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 6);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;               /* sin fondo */
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)                                  /* texto verde */
                    : XLColor.FromHtml(STATE_RED);                                   /* texto rojo */

                // Zebra suave (pares)
                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // 3) Bordes y anchos
            var full = ws.Range(1, 1, r - 1, heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;             /* borde externo */
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;             /* rejilla interna muy fina */

            ws.Columns().AdjustToContents();                                        /* ajusta ancho de columnas */

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== WORD (Open XML SDK) =================== */
        public byte[] GenerarWordPuestos(IEnumerable<PuestoViewModel> datos)
        {
            using var ms = new MemoryStream();
            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = word.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                /* 1) Título */
                var titleP = new Paragraph(new Run(new Text("Reporte de Puestos")));
                titleP.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "200" }                         /* espacio inferior */
                );
                titleP.GetFirstChild<Run>()!.RunProperties = new RunProperties(
                    new Bold(),
                    new Color { Val = HEADER_INK_NOHASH },                             /* color del título si quieres */
                    new FontSize { Val = "28" }                                       /* 14 pt (28 = 14*2) */
                );
                body.Append(titleP);

                /* 2) Tabla */
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableStyle { Val = "TableGrid" },                              /* cuadrícula básica */
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
                )));

                /* 2.1) Encabezados con sombreado */
                string[] heads = { "Código", "Puesto", "Descripcion", "Area", "Fecha creación", "Estado" };
                var headerRow = new TableRow();

                foreach (var h in heads)
                {
                    // Fondo del header (HEADER_BG) + texto en HEADER_INK + negrita
                    var cellProps = new TableCellProperties(
                        new Shading { Fill = HEADER_BG_NOHASH, Val = ShadingPatternValues.Clear, Color = "auto" }
                    );
                    var runProps = new RunProperties(new Bold(), new Color { Val = HEADER_INK_NOHASH });

                    var p = new Paragraph(new Run(new Text(h)));
                    p.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }); /* títulos centrados */
                    p.GetFirstChild<Run>()!.RunProperties = runProps;

                    headerRow.Append(new TableCell(cellProps, p));
                }
                table.Append(headerRow);

                /* 2.2) Filas */
                foreach (var x in datos)
                {
                    var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                    var estadoCap = Capitalizar(estadoRaw);                            // “Activo / Inactivo”

                    // Celdas normales
                    var c1 = new TableCell(new Paragraph(new Run(new Text(x.PUESTO_ID ?? ""))));
                    var c2 = new TableCell(new Paragraph(new Run(new Text(x.PUESTO_NOMBRE ?? ""))));
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.PUESTO_DESCRIPCION ?? ""))));
                    var c4 = new TableCell(new Paragraph(new Run(new Text(x.AREA_NOMBRE ?? ""))));

                    var c5 = new TableCell(new Paragraph(new Run(new Text($"{x.FECHA_CREACION:dd/MM/yyyy}")))); //Cambie el tipo de dato

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c6 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4, c5, c6));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
        }



        //Empleado

        public byte[] GenerarExcelEmpleado(IEnumerable<EmpleadoListItemViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Empleados");

            // Tipografía base de la hoja
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // 1) Encabezados
            string[] heads = { "Código", "Empleado", "Puesto", "CUI", "Telefono", "Genero", "Fecha Ingreso","Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            // 1.1 Estilo del encabezado (color fuerte + títulos centrados)
            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; /* aquí centramos títulos */
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);        /* color del encabezado */
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);        /* color de texto del encabezado */

            // 2) Filas de datos
            int r = 2;
            foreach (var x in datos)
            {
                ws.Cell(r, 1).Value = x.EmpleadoId;
                ws.Cell(r, 2).Value = x.EmpleadoNombre;
                ws.Cell(r, 3).Value = x.PuestoNombre;
                ws.Cell(r, 4).Value = x.CUI;
                ws.Cell(r, 5).Value = x.Telefono;
                ws.Cell(r, 6).Value = x.Genero;
                var dt = x.FechaIngreso.ToDateTime(TimeOnly.MinValue);
                ws.Cell(r, 7).Value = dt;
                ws.Cell(r, 7).Style.DateFormat.Format = "dd/MM/yyyy";

                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 8);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;               /* sin fondo */
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)                                  /* texto verde */
                    : XLColor.FromHtml(STATE_RED);                                   /* texto rojo */

                // Zebra suave (pares)
                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // 3) Bordes y anchos
            var full = ws.Range(1, 1, r - 1, heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;             /* borde externo */
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;             /* rejilla interna muy fina */

            ws.Columns().AdjustToContents();                                        /* ajusta ancho de columnas */

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== WORD (Open XML SDK) =================== */
        public byte[] GenerarWordEmpleado(IEnumerable<EmpleadoListItemViewModel> datos)
        {
            using var ms = new MemoryStream();
            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = word.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                /* 1) Título */
                var titleP = new Paragraph(new Run(new Text("Reporte de Empleados")));
                titleP.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "200" }                         /* espacio inferior */
                );
                titleP.GetFirstChild<Run>()!.RunProperties = new RunProperties(
                    new Bold(),
                    new Color { Val = HEADER_INK_NOHASH },                             /* color del título si quieres */
                    new FontSize { Val = "28" }                                       /* 14 pt (28 = 14*2) */
                );
                body.Append(titleP);

                /* 2) Tabla */
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableStyle { Val = "TableGrid" },                              /* cuadrícula básica */
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
                )));

                /* 2.1) Encabezados con sombreado */
                string[] heads = { "Código", "Empleado", "Puesto", "CUI", "Telefono", "Genero", "Fecha Ingreso", "Estado" };
                var headerRow = new TableRow();

                foreach (var h in heads)
                {
                    // Fondo del header (HEADER_BG) + texto en HEADER_INK + negrita
                    var cellProps = new TableCellProperties(
                        new Shading { Fill = HEADER_BG_NOHASH, Val = ShadingPatternValues.Clear, Color = "auto" }
                    );
                    var runProps = new RunProperties(new Bold(), new Color { Val = HEADER_INK_NOHASH });

                    var p = new Paragraph(new Run(new Text(h)));
                    p.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }); /* títulos centrados */
                    p.GetFirstChild<Run>()!.RunProperties = runProps;

                    headerRow.Append(new TableCell(cellProps, p));
                }
                table.Append(headerRow);

                /* 2.2) Filas */
                foreach (var x in datos)
                {
                    var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                    var estadoCap = Capitalizar(estadoRaw);                            // “Activo / Inactivo”

                    // Celdas normales
                    var c1 = new TableCell(new Paragraph(new Run(new Text(x.EmpleadoId ?? ""))));
                    var c2 = new TableCell(new Paragraph(new Run(new Text(x.EmpleadoNombre ?? ""))));
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.PuestoNombre ?? ""))));
                    var c4 = new TableCell(new Paragraph(new Run(new Text(x.CUI ?? ""))));
                    var c5 = new TableCell(new Paragraph(new Run(new Text(x.Telefono ?? ""))));
                    var c6 = new TableCell(new Paragraph(new Run(new Text(x.Genero ?? ""))));
                    var c7 = new TableCell(new Paragraph(new Run(new Text($"{x.FechaIngreso:dd/MM/yyyy}")))); //Cambie el tipo de dato

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c8 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4, c5, c6,c7,c8));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
        }



        //cliente


        public byte[] GenerarExcelClientes(IEnumerable<ClienteViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Empleados");

            // Tipografía base de la hoja
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // 1) Encabezados
            string[] heads = { "Código", "Cliente", "CUI", "NIT", "Telefono", "Dirreccion", "Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            // 1.1 Estilo del encabezado (color fuerte + títulos centrados)
            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; /* aquí centramos títulos */
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);        /* color del encabezado */
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);        /* color de texto del encabezado */

            // 2) Filas de datos
            int r = 2;
            foreach (var x in datos)
            {
                ws.Cell(r, 1).Value = x.Id;
                ws.Cell(r, 2).Value = x.ClienteNombre;
                ws.Cell(r, 3).Value = x.CUI;
                ws.Cell(r, 4).Value = x.NIT;
                ws.Cell(r, 5).Value = x.TelefonoMovil;
                ws.Cell(r, 6).Value = x.Direccion;
                //var dt = x.FechaIngreso.ToDateTime(TimeOnly.MinValue);
                //ws.Cell(r, 7).Value = dt;
                //ws.Cell(r, 7).Style.DateFormat.Format = "dd/MM/yyyy";

                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 7);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;               /* sin fondo */
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)                                  /* texto verde */
                    : XLColor.FromHtml(STATE_RED);                                   /* texto rojo */

                // Zebra suave (pares)
                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // 3) Bordes y anchos
            var full = ws.Range(1, 1, r - 1, heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;             /* borde externo */
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;             /* rejilla interna muy fina */

            ws.Columns().AdjustToContents();                                        /* ajusta ancho de columnas */

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== WORD (Open XML SDK) =================== */
        public byte[] GenerarWordClientes(IEnumerable<ClienteViewModel> datos)
        {
            using var ms = new MemoryStream();
            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = word.AddMainDocumentPart();
                main.Document = new Document(new Body());
                var body = main.Document.Body!;

                /* 1) Título */
                var titleP = new Paragraph(new Run(new Text("Reporte de Clientes")));
                titleP.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "200" }                         /* espacio inferior */
                );
                titleP.GetFirstChild<Run>()!.RunProperties = new RunProperties(
                    new Bold(),
                    new Color { Val = HEADER_INK_NOHASH },                             /* color del título si quieres */
                    new FontSize { Val = "28" }                                       /* 14 pt (28 = 14*2) */
                );
                body.Append(titleP);

                /* 2) Tabla */
                var table = new Table();
                table.AppendChild(new TableProperties(
                    new TableStyle { Val = "TableGrid" },                              /* cuadrícula básica */
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
                )));

                /* 2.1) Encabezados con sombreado */
                string[] heads = { "Código", "Cliente",  "CUI", "NIT", "Telefono", "Dirreccion","Estado" };
                var headerRow = new TableRow();

                foreach (var h in heads)
                {
                    // Fondo del header (HEADER_BG) + texto en HEADER_INK + negrita
                    var cellProps = new TableCellProperties(
                        new Shading { Fill = HEADER_BG_NOHASH, Val = ShadingPatternValues.Clear, Color = "auto" }
                    );
                    var runProps = new RunProperties(new Bold(), new Color { Val = HEADER_INK_NOHASH });

                    var p = new Paragraph(new Run(new Text(h)));
                    p.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }); /* títulos centrados */
                    p.GetFirstChild<Run>()!.RunProperties = runProps;

                    headerRow.Append(new TableCell(cellProps, p));
                }
                table.Append(headerRow);

                /* 2.2) Filas */
                foreach (var x in datos)
                {
                    var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                    var estadoCap = Capitalizar(estadoRaw);                            // “Activo / Inactivo”

                    // Celdas normales
                    var c1 = new TableCell(new Paragraph(new Run(new Text(x.Id ?? ""))));
                    var c2 = new TableCell(new Paragraph(new Run(new Text(x.ClienteNombre ?? ""))));
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.CUI ?? ""))));
                    var c4 = new TableCell(new Paragraph(new Run(new Text(x.NIT ?? ""))));
                    var c5 = new TableCell(new Paragraph(new Run(new Text(x.TelefonoMovil ?? ""))));
                    var c6 = new TableCell(new Paragraph(new Run(new Text(x.Direccion ?? ""))));
                    //var c7 = new TableCell(new Paragraph(new Run(new Text($"{x.FechaIngreso:dd/MM/yyyy}")))); //Cambie el tipo de dato

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c7 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4, c5, c6, c7));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
        }



        //----------------------------------------------------------------------------------------
        /* =========================================================
 *  PEDIDOS – Export General (usa la VM de tu Index)
 *  Firma de la interfaz:
 *    byte[] GenerarExcelPedidosGeneral(IEnumerable<PedidoListItemViewModel> datos)
 *
 *  Nota:
 *  - Para evitar romper por nombres exactos de propiedades (PedidoId, Empresa, etc.),
 *    usamos helpers por reflexión (GetProp) y casteos seguros (ToInt).
 *  - Si tu tipo se llama diferente (p.ej., PedidoListItemVM), actualiza la INTERFAZ
 *    y este método para que compilen con el nombre correcto.
 * ========================================================= */
        public byte[] GenerarExcelPedidosGeneral(IEnumerable<PedidoListItemViewModel> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Pedidos");
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            // Encabezados
            string[] heads = { "No. Pedido", "Proveedor", "Fecha pedido", "Fecha entrega", "Estado pedido", "Líneas", "Estado" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);

            int r = 2;
            foreach (var x in datos ?? Enumerable.Empty<PedidoListItemViewModel>())
            {
                // === Lectura robusta por nombre de propiedad (no lanzamos excepción si no existe) ===
                var pedidoId = GetProp<string>(x, "PedidoId") ?? "";
                var proveedor = GetProp<string>(x, "Empresa")
                                 ?? GetProp<string>(x, "ProveedorNombre")
                                 ?? "-";
                var fPedTxt = GetProp<string>(x, "FechaPedidoTxt")
                                 ?? FormatMaybeDate(GetProp<object>(x, "FechaPedido"));
                var fEntTxt = GetProp<string>(x, "FechaEntregaTxt")
                                 ?? FormatMaybeDate(GetProp<object>(x, "FechaEntrega"));
                var estNombre = GetProp<string>(x, "EstadoPedidoNombre") ?? "-";
                var lineas = ToInt(GetProp<object>(x, "Lineas"));
                var estadoRaw = (GetProp<string>(x, "ESTADO") ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw); // "Activo" / "Inactivo" / "-"

                ws.Cell(r, 1).Value = pedidoId;
                ws.Cell(r, 2).Value = proveedor;
                ws.Cell(r, 3).Value = string.IsNullOrWhiteSpace(fPedTxt) ? "-" : fPedTxt;
                ws.Cell(r, 4).Value = string.IsNullOrWhiteSpace(fEntTxt) ? "-" : fEntTxt;
                ws.Cell(r, 5).Value = string.IsNullOrWhiteSpace(estNombre) ? "-" : estNombre;
                ws.Cell(r, 6).Value = lineas;

                // Estado con color de texto y centrado (igual que en otras hojas)
                var cEstado = ws.Cell(r, 7);
                cEstado.Value = string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap;
                cEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cEstado.Style.Font.Bold = true;
                cEstado.Style.Fill.BackgroundColor = XLColor.NoColor;
                cEstado.Style.Font.FontColor = (estadoRaw == "ACTIVO")
                    ? XLColor.FromHtml(STATE_GREEN)
                    : XLColor.FromHtml(STATE_RED);

                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // Bordes y anchos
            var full = ws.Range(1, 1, Math.Max(1, r - 1), heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =========================================================
         *  COMPRAS POR PROVEEDOR – Resumen
         * ========================================================= */
        public byte[] GenerarExcelComprasProveedorResumen(IEnumerable<ComprasProveedorResumenVM> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Compras por proveedor");
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            string[] heads = { "Proveedor", "# Pedidos", "Unidades", "Monto", "Ticket medio", "% Part." };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);

            int r = 2;
            foreach (var x in datos ?? Enumerable.Empty<ComprasProveedorResumenVM>())
            {
                ws.Cell(r, 1).Value = x.ProveedorNombre;
                ws.Cell(r, 2).Value = x.CantPedidos;
                ws.Cell(r, 3).Value = x.Unidades;

                var cMonto = ws.Cell(r, 4); cMonto.Value = x.Monto; cMonto.Style.NumberFormat.Format = "#,##0.00";
                var cTick = ws.Cell(r, 5); cTick.Value = x.TicketMedio; cTick.Style.NumberFormat.Format = "#,##0.00";
                var cPart = ws.Cell(r, 6); cPart.Value = x.ParticipacionPorc / 100m; cPart.Style.NumberFormat.Format = "0.00%";

                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            var full = ws.Range(1, 1, Math.Max(1, r - 1), heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =========================================================
         *  COMPRAS POR PROVEEDOR – Detalle (por pedido)
         *  (lo puedes usar cuando agregues drill-down)
         * ========================================================= */
        public byte[] GenerarExcelComprasProveedorDetalle(IEnumerable<ComprasProveedorDetalleVM> datos)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Detalle por pedido");
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            string[] heads = { "No. Pedido", "Fecha pedido", "Fecha recibido", "Líneas", "Monto" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(1, c + 1).Value = heads[c];

            var header = ws.Range(1, 1, 1, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);

            int r = 2;
            foreach (var x in datos ?? Enumerable.Empty<ComprasProveedorDetalleVM>())
            {
                ws.Cell(r, 1).Value = x.PedidoId;
                ws.Cell(r, 2).Value = x.FechaPedido; ws.Cell(r, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                if (x.FechaRecibido.HasValue)
                {
                    ws.Cell(r, 3).Value = x.FechaRecibido.Value;
                    ws.Cell(r, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                }
                else ws.Cell(r, 3).Value = "-";

                ws.Cell(r, 4).Value = x.Lineas;

                var cMonto = ws.Cell(r, 5);
                cMonto.Value = x.Monto;
                cMonto.Style.NumberFormat.Format = "#,##0.00";

                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            var full = ws.Range(1, 1, Math.Max(1, r - 1), heads.Length);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =========================================================
         *  PEDIDO CERRADO – Excel (encabezado + detalle + total)
         * ========================================================= */
        public byte[] GenerarExcelPedidoCerrado(PedidoCerradoVM pedido)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet($"Pedido {pedido.PedidoId}");
            ws.Style.Font.FontName = "Calibri";
            ws.Style.Font.FontSize = 11;

            int r = 1;

            // Título
            ws.Cell(r, 1).Value = "Orden cerrada";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 14;
            r += 2;

            // Encabezado (par clave/valor)
            ws.Cell(r, 1).Value = "No. Pedido:"; ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = pedido.PedidoId;

            ws.Cell(r, 3).Value = "Proveedor:"; ws.Cell(r, 3).Style.Font.Bold = true;
            ws.Cell(r, 4).Value = pedido.ProveedorNombre;
            r++;

            ws.Cell(r, 1).Value = "Fecha pedido:"; ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = pedido.FechaPedido; ws.Cell(r, 2).Style.DateFormat.Format = "dd/MM/yyyy";

            ws.Cell(r, 3).Value = "Fecha recibido:"; ws.Cell(r, 3).Style.Font.Bold = true;
            if (pedido.FechaRecibido.HasValue)
            {
                ws.Cell(r, 4).Value = pedido.FechaRecibido.Value;
                ws.Cell(r, 4).Style.DateFormat.Format = "dd/MM/yyyy";
            }
            else ws.Cell(r, 4).Value = "-";
            r++;

            ws.Cell(r, 1).Value = "Observación:"; ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = string.IsNullOrWhiteSpace(pedido.Observacion) ? "-" : pedido.Observacion;
            r++;

            ws.Cell(r, 1).Value = "Estado:"; ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = pedido.EstadoNombre;
            r += 2;

            // Tabla detalle
            string[] heads = { "Código", "Producto", "Cant.", "Precio compra", "Subtotal", "Vencimiento" };
            for (int c = 0; c < heads.Length; c++)
                ws.Cell(r, c + 1).Value = heads[c];

            var header = ws.Range(r, 1, r, heads.Length);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml(HEADER_BG);
            header.Style.Font.FontColor = XLColor.FromHtml(HEADER_INK);
            r++;

            foreach (var l in pedido.Lineas ?? Enumerable.Empty<PedidoCerradoLineaVM>())
            {
                ws.Cell(r, 1).Value = l.Codigo ?? l.ProductoId;
                ws.Cell(r, 2).Value = l.Nombre;
                ws.Cell(r, 3).Value = l.Cantidad;

                var cPrec = ws.Cell(r, 4); cPrec.Value = l.PrecioCompra; cPrec.Style.NumberFormat.Format = "#,##0.00";
                var cSub = ws.Cell(r, 5); cSub.Value = l.Subtotal; cSub.Style.NumberFormat.Format = "#,##0.00";

                if (l.FechaVencimiento.HasValue)
                {
                    ws.Cell(r, 6).Value = new DateTime(l.FechaVencimiento.Value.Year, l.FechaVencimiento.Value.Month, l.FechaVencimiento.Value.Day);
                    ws.Cell(r, 6).Style.DateFormat.Format = "dd/MM/yyyy";
                }
                else ws.Cell(r, 6).Value = "-";

                if ((r % 2) == 0)
                    ws.Range(r, 1, r, heads.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(ZEBRA_BG);

                r++;
            }

            // Total
            ws.Cell(r, 4).Value = "Total"; ws.Cell(r, 4).Style.Font.Bold = true; ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            var cTot = ws.Cell(r, 5); cTot.Value = pedido.Total; cTot.Style.NumberFormat.Format = "#,##0.00"; cTot.Style.Font.Bold = true;

            // Bordes y anchos
            var lastRow = r;
            var full = ws.Range(1, 1, lastRow, 6);
            full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            full.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /* =================== HELPERS LOCALES (solo para este archivo) =================== */

        // Obtiene propiedad por reflexión (seguro si no existe), como object
        private static T? GetProp<T>(object obj, string propName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propName)) return default;
            var pi = obj.GetType().GetProperty(propName);
            if (pi == null) return default;

            var val = pi.GetValue(obj);
            if (val == null) return default;

            try
            {
                if (typeof(T) == typeof(string))
                    return (T)(object)val.ToString()!;
                if (val is T t) return t;

                return (T)Convert.ChangeType(val, typeof(T));
            }
            catch { return default; }
        }

        // Convierte object → int de forma segura
        private static int ToInt(object? v)
        {
            if (v == null) return 0;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is decimal d) return (int)d;
            if (v is double db) return (int)db;
            _ = int.TryParse(v.ToString(), out var n);
            return n;
        }

        // Intenta formatear un DateTime/DateOnly → "dd/MM/yyyy"; si no, string.ToString() o ""
        private static string FormatMaybeDate(object? v)
        {
            if (v == null) return "";
            if (v is DateTime dt) return dt.ToString("dd/MM/yyyy");
            if (v is DateOnly d) return new DateTime(d.Year, d.Month, d.Day).ToString("dd/MM/yyyy");
            return v.ToString() ?? "";
        }

















        /* =================== HELPERS =================== */

        /// <summary>
        /// Devuelve "Activo" / "Inactivo" a partir de "ACTIVO" / "INACTIVO".
        /// Si está vacío, devuelve "-".
        /// </summary>
        private static string Capitalizar(string mayus)
        {
            if (string.IsNullOrWhiteSpace(mayus)) return "-";
            var s = mayus.Trim().ToLowerInvariant();
            return char.ToUpper(s[0]) + s[1..];
        }
    }
}




