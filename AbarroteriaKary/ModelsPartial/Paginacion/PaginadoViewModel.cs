using System;
using System.Collections.Generic;

namespace AbarroteriaKary.ModelsPartial.Paginacion
{
    /// <summary>
    /// Base no genérica para que el _Pager.cshtml funcione con cualquier lista.
    /// </summary>
    public class PaginadoBase
    {
        public int Page { get; set; } = 1;                 // página actual (1-based)
        public int PageSize { get; set; } = 10;            // tamaño de página
        public int TotalCount { get; set; }                // total de registros
        public int TotalPages { get; set; }                // total de páginas
        public bool HasPrev => Page > 1;
        public bool HasNext => Page < TotalPages;

        /// <summary>
        /// Filtros/valores del querystring que se deben preservar en los links del paginador.
        /// Ej.: estado, q, fDesde, fHasta, etc.
        /// </summary>
        public Dictionary<string, object?> RouteValues { get; set; } = new();
    }

    /// <summary>
    /// Paginado genérico con Items.
    /// </summary>
    public class PaginadoViewModel<T> : PaginadoBase
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    }
}
