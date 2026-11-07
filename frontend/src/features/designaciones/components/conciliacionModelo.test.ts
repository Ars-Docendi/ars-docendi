import { describe, expect, it } from "vitest";

import type { PedidoConciliacion } from "../api/conciliacionApi";
import { resultado, type DocenteConciliacion } from "./conciliacionModelo";

const v = (cargo: string, horas: number) => ({
  cargo,
  dedicacion: "Simple",
  horas,
  horasInvestigacion: null,
  horasExternas: null,
});

const pedido = (datos: Partial<PedidoConciliacion>): PedidoConciliacion => ({
  id: "x",
  numero: "P-x",
  version: 1,
  novedad: "Alta",
  estado: "en_lote",
  materiaId: "m",
  materia: "M",
  carreraId: "c",
  carrera: "C",
  hoy: null,
  queda: null,
  justificacion: null,
  tipoBaja: null,
  tipoBajaDetalle: null,
  motivoRechazo: null,
  sincronizadoEn: null,
  sincronizadoPor: null,
  acciones: ["sincronizar"],
  ...datos,
});

describe("Cómo queda el docente al sincronizar", () => {
  it("aplica Alta, Cambio y Baja en lote sobre las vigentes e ignora los rechazados", () => {
    const docente: DocenteConciliacion = {
      personaId: "p",
      nombre: "Ana",
      apellido: "Paz",
      cuil: null,
      documento: "1",
      personaNueva: false,
      vigentes: [
        {
          materiaId: "redes",
          materia: "Redes",
          carreraId: "c",
          carrera: "C",
          valores: v("JTP", 8),
        },
        { materiaId: "so", materia: "SO", carreraId: "c", carrera: "C", valores: v("Adjunto", 10) },
        { materiaId: "bd", materia: "BD", carreraId: "c", carrera: "C", valores: v("JTP", 10) },
      ],
      pedidos: [
        pedido({ id: "1", novedad: "Baja", materiaId: "redes" }),
        pedido({
          id: "2",
          novedad: "Cambio de cargo o dedicación",
          materiaId: "so",
          queda: v("Asociado", 20),
        }),
        pedido({ id: "3", novedad: "Alta", materiaId: "fisica", queda: v("Ayudante", 4) }),
        pedido({
          id: "4",
          novedad: "Cambio de cargo o dedicación",
          materiaId: "bd",
          estado: "no_aprobado",
          queda: v("Adjunto", 10),
        }),
      ],
    };

    expect(
      Object.fromEntries(resultado(docente).map((d) => [d.materiaId, d.valores.cargo])),
    ).toEqual({ so: "Asociado", bd: "JTP", fisica: "Ayudante" });
  });
});
