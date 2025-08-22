using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("AREA_ID", Name = "IX_PUESTO_AREA")]
public partial class PUESTO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PUESTO_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string PUESTO_NOMBRE { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? PUESTO_DESCRIPCION { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string AREA_ID { get; set; } = null!;

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

    [ForeignKey("AREA_ID")]
    [InverseProperty("PUESTO")]
    public virtual AREA AREA { get; set; } = null!;

    [InverseProperty("PUESTO")]
    public virtual ICollection<EMPLEADO> EMPLEADO { get; set; } = new List<EMPLEADO>();
}
