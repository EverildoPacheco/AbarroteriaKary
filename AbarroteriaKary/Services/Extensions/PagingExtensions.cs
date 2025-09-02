using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.ModelsPartial.Paginacion;

namespace AbarroteriaKary.Services.Extensions
{
    public static class PagingExtensions
    {
        /// <summary>
        /// Pagina un IQueryable ya ordenado (IMPORTANTE: siempre ordenar antes).
        /// </summary>
        public static async Task<PaginadoViewModel<T>> ToPagedAsync<T>(
            this IQueryable<T> query,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            if (pageSize <= 0) pageSize = 10;
            if (page <= 0) page = 1;

            var total = await query.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PaginadoViewModel<T>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = totalPages,
                Items = items
            };
        }
    }
}
