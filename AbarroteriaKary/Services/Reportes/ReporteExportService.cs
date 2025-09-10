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
            string[] heads = { "Código", "Área", "Fecha creación", "Estado" };
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
                ws.Cell(r, 3).Value = x.FechaCreacion;
                ws.Cell(r, 3).Style.DateFormat.Format = "dd/MM/yyyy";               /* formato de fecha */

                // Estado: solo color de texto (sin fondo), centrado y en negrita
                var estadoRaw = (x.ESTADO ?? "").Trim().ToUpperInvariant();
                var estadoCap = Capitalizar(estadoRaw);                              /* “Activo / Inactivo” */
                var cEstado = ws.Cell(r, 4);
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
                string[] heads = { "Código", "Área", "Fecha creación", "Estado" };
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
                    var c3 = new TableCell(new Paragraph(new Run(new Text(x.FechaCreacion.ToString("dd/MM/yyyy")))));

                    // Celda Estado: solo color de texto
                    var estadoRunProps = new RunProperties(new Bold(),
                        new Color { Val = (estadoRaw == "ACTIVO") ? STATE_GREEN_NOHASH : STATE_RED_NOHASH });

                    var pEstado = new Paragraph(new Run(new Text(string.IsNullOrWhiteSpace(estadoRaw) ? "-" : estadoCap)));
                    pEstado.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    pEstado.GetFirstChild<Run>()!.RunProperties = estadoRunProps;
                    var c4 = new TableCell(pEstado);

                    table.Append(new TableRow(c1, c2, c3, c4));
                }

                body.Append(table);
                main.Document.Save();
            }
            return ms.ToArray();
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




//using System.Collections.Generic;
//using System.IO;
//using ClosedXML.Excel;
//using AbarroteriaKary.ModelsPartial;
////using Xceed.Words.NET;     // DocX (clase DocX)
//using DocumentFormat.OpenXml;
//using DocumentFormat.OpenXml.Packaging;
//using DocumentFormat.OpenXml.Wordprocessing;

////using AbarroteriaKary.ModelsPartial.Reportes;


//namespace AbarroteriaKary.Services.Reportes
//{
//    public class ReporteExportService : IReporteExportService
//    {
//        public byte[] GenerarExcelAreas(IEnumerable<AreasViewModel> datos)
//        {
//            using var wb = new XLWorkbook();
//            var ws = wb.AddWorksheet("Áreas");

//            // Encabezados
//            string[] heads = { "Código", "Área", "Fecha creación", "Estado" };
//            for (int c = 0; c < heads.Length; c++) ws.Cell(1, c + 1).Value = heads[c];

//            // Estilo encabezado (su paleta)
//            var header = ws.Range(1, 1, 1, heads.Length);
//            header.Style.Font.Bold = true;
//            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#9fc095");
//            header.Style.Font.FontColor = XLColor.White;

//            int r = 2;
//            foreach (var x in datos)
//            {
//                ws.Cell(r, 1).Value = x.areaId;
//                ws.Cell(r, 2).Value = x.areaNombre;
//                ws.Cell(r, 3).Value = x.FechaCreacion;
//                ws.Cell(r, 3).Style.DateFormat.Format = "dd/MM/yyyy";
//                ws.Cell(r, 4).Value = x.estadoArea;
//                r++;
//            }

//            ws.Columns().AdjustToContents();

//            using var ms = new MemoryStream();
//            wb.SaveAs(ms);
//            return ms.ToArray();
//        }

//        public byte[] GenerarWordAreas(IEnumerable<AreasViewModel> datos)
//        {
//            using var ms = new MemoryStream();
//            using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
//            {
//                var main = word.AddMainDocumentPart();
//                main.Document = new Document(new Body());
//                var body = main.Document.Body!;

//                // Título "Reporte de Áreas"
//                var title = new Paragraph(new Run(new Text("Reporte de Áreas")));
//                title.ParagraphProperties = new ParagraphProperties(
//                    new Justification { Val = JustificationValues.Left },
//                    new SpacingBetweenLines { After = "200" } // 20 * 10 = 200 twips
//                );
//                title.GetFirstChild<Run>()!.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" }); // 14pt
//                body.Append(title);

//                // Tabla
//                var table = new Table();

//                // Bordes sencillos tipo grid
//                var tblProps = new TableProperties(
//                    new TableStyle { Val = "TableGrid" },
//                    new TableBorders(
//                        new TopBorder { Val = BorderValues.Single, Size = 6 },
//                        new LeftBorder { Val = BorderValues.Single, Size = 6 },
//                        new BottomBorder { Val = BorderValues.Single, Size = 6 },
//                        new RightBorder { Val = BorderValues.Single, Size = 6 },
//                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
//                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }
//                    )
//                );
//                table.AppendChild(tblProps);

//                // Encabezados
//                var heads = new[] { "Código", "Área", "Fecha creación", "Estado" };
//                var headerRow = new TableRow();
//                foreach (var h in heads)
//                {
//                    var cell = new TableCell(new Paragraph(new Run(new Text(h))));
//                    cell.Descendants<Run>().First().RunProperties = new RunProperties(new Bold());
//                    headerRow.Append(cell);
//                }
//                table.Append(headerRow);

//                // Filas
//                foreach (var x in datos)
//                {
//                    var row = new TableRow(
//                        new TableCell(new Paragraph(new Run(new Text(x.areaId ?? "")))),
//                        new TableCell(new Paragraph(new Run(new Text(x.areaNombre ?? "")))),
//                        new TableCell(new Paragraph(new Run(new Text(x.FechaCreacion.ToString("dd/MM/yyyy"))))),
//                        new TableCell(new Paragraph(new Run(new Text(x.estadoArea ?? ""))))
//                    );
//                    table.Append(row);
//                }

//                body.Append(table);
//                main.Document.Save();
//            }
//            return ms.ToArray();
//        }
//    }

//}
