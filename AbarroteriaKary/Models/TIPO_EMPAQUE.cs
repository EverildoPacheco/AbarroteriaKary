using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class TIPO_EMPAQUE
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string TIPO_EMPAQUE_ID { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string TIPO_EMPAQUE_NOMBRE { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? TIPO_EMPAQUE_DESCRIPCION { get; set; }

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

    [InverseProperty("TIPO_EMPAQUE")]
    public virtual ICollection<PRODUCTO> PRODUCTO { get; set; } = new List<PRODUCTO>();
}
