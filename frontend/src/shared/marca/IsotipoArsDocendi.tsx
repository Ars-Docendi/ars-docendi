/**
 * Isotipo de Ars Docendi: la "A" y la "D" entrelazadas con un birrete.
 * Monocromo en `currentColor` para que tome el color del contenedor.
 */
export function IsotipoArsDocendi({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="390 120 760 500"
      fill="currentColor"
      aria-hidden="true"
      focusable="false"
    >
      {/* A: patas y vértice */}
      <path d="M397 587 L622 128 L833 571 L805 613 L621 230 L445 587 Z" />
      {/* A: travesaño */}
      <path d="M570 410 L708 410 L727 455 L549 455 Z" />
      {/* D: barra inferior, curva y barra superior */}
      <path d="M805 613 L930 613 A215 224 0 0 0 930 165 L712 165 L733 208 L930 208 A174 181.5 0 0 1 930 571 L833 571 Z" />
      {/* Birrete: tabla, copa y borla */}
      <path d="M795 368 L915 312 L1037 366 L915 422 Z" />
      <path d="M841 407 L915 440 L988 407 L988 452 C960 470 935 482 915 482 C895 482 868 470 841 452 Z" />
      <path d="M1019 372 h6 v42 h-6 Z" />
      <path d="M1022 410 L1029 430 L1036 462 C1036 471 1014 471 1014 462 L1016 430 Z" />
    </svg>
  );
}
