using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class METODO_PAGO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string METODO_PAGO_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string METODO_PAGO_NOMBRE { get; set; } = null!;

    [StringLength(250)]
    [Unicode(false)]
    public string? METODO_PAGO_DESCRIPCION { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string CREADO_POR { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA_CREACION { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? MODIFICADO_POR { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_MODIFICACION { get; set; }

    public bool ELIMINADO { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? ELIMINADO_POR { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_ELIMINACION { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ESTADO { get; set; } = null!;

    [InverseProperty("METODO_PAGO")]
    public virtual ICollection<RECIBO> RECIBO { get; set; } = new List<RECIBO>();
}
