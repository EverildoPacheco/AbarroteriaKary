using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AbarroteriaKary.ModelsPartial
{
    // ============================================================
    // CAJA - ViewModels para Ventas (Apertura / Cierre / Estado)
    // Tablas: CAJA_SESION, MOVIMIENTO_CAJA
    // Uso: Encabezar la pantalla de Ventas con el botón "Caja" y
    //      sus modales (Aperturar / Cerrar). Controla habilitar
    //      "Nueva venta" solo con sesión abierta.
    // ============================================================

    #region Constantes / Helpers de Dominio

    public static class CajaSesionEstados
    {
        public const string Abierta = "ABIERTA";
        public const string Cerrada = "CERRADA";

        public static bool EsValido(string? v)
            => string.Equals(v, Abierta, StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, Cerrada, StringComparison.OrdinalIgnoreCase);
    }

    public static class CajaRegistroEstados
    {
        public const string Activo = "ACTIVO";
        public const string Inactivo = "INACTIVO";
    }

    #endregion

    // ============================================================
    // 1) VM para pintar el estado actual de Caja en la pantalla de Ventas
    //    (sirve para saber si está ABIERTO/CERRADO y habilitar “Nueva venta”)
    // ============================================================
    public class CajaEstadoVM
    {
        [Display(Name = "Id Caja")]
        public string CajaId { get; set; } = string.Empty;

        [Display(Name = "Nombre de caja")]
        public string? CajaNombre { get; set; }

        [Display(Name = "Id Sesión")]
        public string? SesionId { get; set; } // null -> no hay sesión abierta

        [Display(Name = "Estado sesión")]
        public string EstadoSesion
        {
            get => SesionAbierta ? CajaSesionEstados.Abierta : CajaSesionEstados.Cerrada;
            set => SesionAbierta = string.Equals(value, CajaSesionEstados.Abierta, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Flag directo para la UI (habilita "Nueva venta" cuando true).
        /// </summary>
        [Display(Name = "¿Sesión abierta?")]
        public bool SesionAbierta { get; set; }

        // ---- Información de apertura (si está abierta) ----
        [Display(Name = "Fecha de apertura")]
        public DateTime? FechaApertura { get; set; }

        [Display(Name = "Usuario apertura (id)")]
        public string? UsuarioAperturaId { get; set; }

        [Display(Name = "Usuario apertura (nombre)")]
        public string? UsuarioAperturaNombre { get; set; }

        [Display(Name = "Monto inicial")]
        [DataType(DataType.Currency)]
        public decimal? MontoInicial { get; set; }

        [Display(Name = "Nota apertura")]
        public string? NotaApertura { get; set; }

        // ---- Totales corrientes de movimientos (para cinta/preview) ----
        [Display(Name = "Ingresos (efectivo)")]
        [DataType(DataType.Currency)]
        public decimal TotalIngresos { get; set; }

        [Display(Name = "Egresos")]
        [DataType(DataType.Currency)]
        public decimal TotalEgresos { get; set; }

        /// <summary>
        /// Saldo esperado: MontoInicial + Ingresos - Egresos (sólo informativo).
        /// </summary>
        [Display(Name = "Saldo esperado")]
        [DataType(DataType.Currency)]
        public decimal SaldoEsperado => Math.Round((MontoInicial ?? 0m) + TotalIngresos - TotalEgresos, 2);

        // ---- Auditoría de la sesión (si aplica) ----
        [ValidateNever] public AuditoriaViewModel Auditoria { get; set; } = new();

        // Estética (badge/clase)
        [ValidateNever]
        public string EstadoBadgeCss => SesionAbierta ? "badge-estado--activo" : "badge-estado--inactivo";
    }

    // ============================================================
    // 2) VM para el modal de APERTURA de caja (imagen 2)
    //    Mapea a CAJA_SESION: FECHA_APERTURA, USUARIO_APERTURA_ID,
    //    MONTO_INICIAL, NOTAAPERTURA. Estado = 'ABIERTA'
    // ============================================================
    public class CajaAperturaViewModel
    {
        // ---- Identificadores ----
        [Display(Name = "Id Sesión (preview)")]
        public string? SesionId { get; set; } // Preview con correlativo (Peek). El definitivo se genera en POST.

        [Required]
        [Display(Name = "Id Caja")]
        public string CajaId
        {
            get => _cajaId;
            set => _cajaId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _cajaId = string.Empty;

        [Display(Name = "Nombre de caja")]
        public string? CajaNombre { get; set; }

        // ---- Apertura ----
        [Display(Name = "Fecha de apertura")]
        public DateTime FechaApertura { get; set; } = DateTime.Now; // default en UI; en BD también tiene DEFAULT GETDATE()

        [Required(ErrorMessage = "Usuario de apertura es requerido.")]
        [Display(Name = "Usuario apertura (id)")]
        public string UsuarioAperturaId
        {
            get => _usuarioAperturaId;
            set => _usuarioAperturaId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _usuarioAperturaId = string.Empty;

        [Display(Name = "Usuario apertura (nombre)")]
        public string? UsuarioAperturaNombre { get; set; } // Sólo display

        [Required(ErrorMessage = "Ingrese el efectivo de apertura.")]
        [Range(0, 999999999999.99, ErrorMessage = "El monto inicial no puede ser negativo.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Efectivo de apertura")]
        public decimal MontoInicial { get; set; }

        [StringLength(250, ErrorMessage = "Máximo {1} caracteres.")]
        [Display(Name = "Nota")]
        public string? NotaApertura
        {
            get => _notaApertura;
            set => _notaApertura = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _notaApertura;

        // ---- Estado sesión fijo a ABIERTA / validación defensiva ----
        [Display(Name = "Estado sesión")]
        public string EstadoSesion { get; private set; } = CajaSesionEstados.Abierta;

        // ---- Auditoría simple de creación ----
        [ValidateNever] public AuditoriaViewModel Auditoria { get; set; } = new();

        // Validaciones de negocio adicionales (si se desea implementar IValidatableObject)
        public IEnumerable<ValidationResult> Validate(ValidationContext _)
        {
            // (ejemplo) nada por ahora: DataAnnotations cubren las reglas
            yield break;
        }
    }

    // ============================================================
    // 3) VM para el modal de CIERRE de caja (imagen 8)
    //    Mapea a CAJA_SESION: FECHA_CIERRE, USUARIO_CIERRE_ID,
    //    MONTO_FINAL, NOTACIERRE. Valida que exista sesión ABIERTA.
    // ============================================================
    public class CajaCierreViewModel : IValidatableObject
    {
        // ---- Identificadores ----
        [Required]
        [Display(Name = "Id Sesión")]
        public string SesionId
        {
            get => _sesionId;
            set => _sesionId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _sesionId = string.Empty;

        [Required]
        [Display(Name = "Id Caja")]
        public string CajaId
        {
            get => _cajaId;
            set => _cajaId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _cajaId = string.Empty;

        [Display(Name = "Nombre de caja")]
        public string? CajaNombre { get; set; }

        // ---- Datos de apertura (solo lectura, informativos) ----
        [Display(Name = "Abierta desde")]
        public DateTime FechaApertura { get; set; }

        [Display(Name = "Monto inicial")]
        [DataType(DataType.Currency)]
        public decimal MontoInicial { get; set; }

        [Display(Name = "Usuario apertura (id)")]
        public string? UsuarioAperturaId { get; set; }

        [Display(Name = "Usuario apertura (nombre)")]
        public string? UsuarioAperturaNombre { get; set; }

        // ---- Cierre ----
        [Display(Name = "Fecha de cierre")]
        public DateTime FechaCierre { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Usuario de cierre es requerido.")]
        [Display(Name = "Usuario cierre (id)")]
        public string UsuarioCierreId
        {
            get => _usuarioCierreId;
            set => _usuarioCierreId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _usuarioCierreId = string.Empty;

        [StringLength(250)]
        [Display(Name = "Nota de cierre")]
        public string? NotaCierre
        {
            get => _notaCierre;
            set => _notaCierre = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _notaCierre;



        [ValidateNever]
        public string? UsuarioCierreNombre { get; set; }






        [Required(ErrorMessage = "Ingrese el monto final contado en caja.")]
        [Range(0, 999999999999.99, ErrorMessage = "El monto final no puede ser negativo.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Total efectivo en caja")]
        public decimal MontoFinal { get; set; }

        // ---- Totales calculados para conciliación (solo display) ----
        [Display(Name = "Total ventas (efectivo)")]
        [DataType(DataType.Currency)]
        public decimal TotalVentasEfectivo { get; set; } // si lo separas por método

        [Display(Name = "Ingresos")]
        [DataType(DataType.Currency)]
        public decimal TotalIngresos { get; set; }

        [Display(Name = "Egresos")]
        [DataType(DataType.Currency)]
        public decimal TotalEgresos { get; set; }

        [Display(Name = "Saldo esperado")]
        [DataType(DataType.Currency)]
        public decimal SaldoEsperado { get; set; } // MontoInicial + Ingresos - Egresos

        [Display(Name = "Diferencia (Final - Esperado)")]
        [DataType(DataType.Currency)]
        public decimal Diferencia => Math.Round(MontoFinal - SaldoEsperado, 2);

        // ---- Auditoría / Estado ----
        [Display(Name = "Estado sesión")]
        public string EstadoSesion { get; set; } = CajaSesionEstados.Cerrada; // fija en cierre

        [ValidateNever] public AuditoriaViewModel Auditoria { get; set; } = new();

        // ---- Validaciones de negocio ----
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MontoFinal < 0)
                yield return new ValidationResult("El monto final no puede ser negativo.", new[] { nameof(MontoFinal) });

            if (!CajaSesionEstados.EsValido(EstadoSesion) || EstadoSesion != CajaSesionEstados.Cerrada)
                yield return new ValidationResult("Estado de cierre inválido.", new[] { nameof(EstadoSesion) });
        }
    }

    // ============================================================
    // 4) VM para listar/mostrar movimientos de caja dentro de una sesión
    //    (para un panel/cinta en el modal de cierre o detalle)
    // ============================================================
    public class MovimientoCajaItemVM
    {
        [Display(Name = "Id mov.")]
        public string MovimientoId { get; set; } = string.Empty;

        [Display(Name = "Sesión")]
        public string SesionId { get; set; } = string.Empty;

        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; }

        [Display(Name = "Tipo")]
        public string Tipo { get; set; } = "INGRESO"; // INGRESO / EGRESO

        [DataType(DataType.Currency)]
        [Range(0, 999999999999.99)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        [Display(Name = "Referencia")]
        public string? Referencia { get; set; } // p.ej. VENTA_ID

        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        [ValidateNever] public AuditoriaViewModel Auditoria { get; set; } = new();
    }

    // ============================================================
    // 5) VM contenedor para la barra de Ventas (menú Caja)
    //    Útil si quieres pasar en un sólo modelo todo lo necesario
    //    al encabezado: estado + datos para abrir/cerrar.
    // ============================================================
    public class CajaEnVentasVM
    {
        [ValidateNever] public CajaEstadoVM Estado { get; set; } = new();

        // Estos se usan para inicializar los modales (GET):
        [ValidateNever] public CajaAperturaViewModel Apertura { get; set; } = new();
        [ValidateNever] public CajaCierreViewModel Cierre { get; set; } = new();

        // Reglas de UI rápidas
        public bool PuedeAperturar => !Estado.SesionAbierta;
        public bool PuedeCerrar => Estado.SesionAbierta;
        public bool PuedeNuevaVenta => Estado.SesionAbierta;
    }

  
}
