import { Link } from "react-router-dom";

import { IsotipoArsDocendi } from "../auth/IsotipoArsDocendi";
import "./TerminosPage.css";

/** Términos de uso y política de privacidad: página pública, fuera del shell. */
export function TerminosPage() {
  return (
    <div className="terminos-page adoc-ui">
      <header className="terminos-header">
        <Link to="/login" className="terminos-marca">
          <IsotipoArsDocendi className="terminos-isotipo" />
          <span>Ars Docendi</span>
        </Link>
        <Link to="/login" className="terminos-volver">
          Volver al ingreso
        </Link>
      </header>

      <main className="terminos-contenido">
        <h1>Términos de uso y política de privacidad</h1>
        <p className="terminos-vigencia">Última actualización: 27 de octubre de 2026</p>

        <section>
          <h2>1. Alcance y aceptación</h2>
          <p>
            Ars Docendi es el sistema de gestión docente del Departamento de Ingeniería e
            Investigaciones Tecnológicas de la Universidad Nacional de La Matanza (UNLaM). Sólo
            pueden usarlo las personas que la administración del sistema dio de alta, y únicamente
            para la gestión académica del departamento. Al ingresar aceptás estos términos.
          </p>
        </section>

        <section>
          <h2>2. Responsable de los datos</h2>
          <p>
            La Universidad Nacional de La Matanza, a través del Departamento de Ingeniería e
            Investigaciones Tecnológicas, es responsable de las bases de datos del sistema y del
            tratamiento de los datos personales que contienen, en los términos de la Ley N° 25.326
            de Protección de los Datos Personales.
          </p>
        </section>

        <section>
          <h2>3. Datos que se tratan</h2>
          <ul>
            <li>
              Identificación: nombre y apellido, documento, CUIL, fecha de nacimiento y legajo.
            </li>
            <li>Contacto: correo electrónico y teléfono.</li>
            <li>
              Cuenta de acceso: el correo de tu cuenta Microsoft y los identificadores que vinculan
              esa cuenta con tu usuario. Ars Docendi no recibe ni guarda tu contraseña.
            </li>
            <li>
              Perfil docente: currículum, formación, experiencia, certificaciones, proyectos y áreas
              de experticia.
            </li>
            <li>
              Gestión académica: pedidos de designación, designaciones vigentes e históricas,
              reservas de aula y tareas.
            </li>
            <li>
              Documentación adjunta: currículum, imágenes del documento de identidad y
              justificativos que se suben a los trámites.
            </li>
            <li>Consultas que hacés al asistente conversacional.</li>
            <li>Registro de auditoría: qué acciones se hicieron en el sistema, quién y cuándo.</li>
          </ul>
        </section>

        <section>
          <h2>4. Finalidad</h2>
          <p>
            Los datos se usan exclusivamente para tramitar designaciones docentes, administrar
            reservas de aula y tareas, mantener el perfil docente y dejar constancia de las acciones
            realizadas. No se usan con otros fines ni se ceden a terceros, salvo que una ley o una
            autoridad competente lo exija.
          </p>
        </section>

        <section>
          <h2>5. Datos obligatorios</h2>
          <p>
            Los datos de identificación y la documentación que requiere cada trámite son necesarios
            para tramitarlo: sin ellos, el trámite no puede avanzar. Los datos del perfil docente
            que no exija un trámite son opcionales.
          </p>
        </section>

        <section>
          <h2>6. Quién accede a los datos</h2>
          <p>
            Sólo el personal autorizado, según su rol y el ámbito de materias y carreras que tiene
            asignado. Microsoft interviene únicamente para verificar tu identidad al ingresar. El
            asistente conversacional funciona en servidores propios de la Universidad y no envía tus
            datos a servicios externos.
          </p>
        </section>

        <section>
          <h2>7. Seguridad y conservación</h2>
          <p>
            El acceso se controla por roles y permisos, la comunicación viaja cifrada, los archivos
            adjuntos se analizan antes de quedar disponibles y cada acción queda registrada en la
            auditoría. Los datos se conservan mientras sean necesarios para la finalidad que
            justificó su recolección y según las normas de archivo de la Universidad.
          </p>
        </section>

        <section>
          <h2>8. Tus derechos</h2>
          <p>
            Podés solicitar el acceso a tus datos, y su rectificación, actualización o supresión
            cuando corresponda, comunicándote con Secretaría Académica. El pedido de acceso se
            responde dentro de los diez días corridos y los de rectificación, actualización o
            supresión, dentro de los cinco días hábiles, conforme a los artículos 14 y 16 de la Ley
            N° 25.326.
          </p>
          <p className="terminos-leyenda">
            El titular de los datos personales tiene la facultad de ejercer el derecho de acceso a
            los mismos en forma gratuita a intervalos no inferiores a seis meses, salvo que se
            acredite un interés legítimo al efecto conforme lo establecido en el artículo 14, inciso
            3 de la Ley N° 25.326. La Agencia de Acceso a la Información Pública, en su carácter de
            Órgano de Control de la Ley N° 25.326, tiene la atribución de atender las denuncias y
            reclamos que interpongan quienes resulten afectados en sus derechos por incumplimiento
            de las normas vigentes en materia de protección de datos personales.
          </p>
        </section>

        <section>
          <h2>9. Tus obligaciones</h2>
          <ul>
            <li>Usar tu propia cuenta y no compartirla.</li>
            <li>
              Usar los datos de otras personas que veas en el sistema sólo para la gestión académica
              y no divulgarlos.
            </li>
            <li>Cargar información veraz y mantenerla actualizada.</li>
            <li>
              Avisar a Secretaría Académica si detectás un acceso indebido o un error en los datos.
            </li>
          </ul>
        </section>

        <section>
          <h2>10. Cambios</h2>
          <p>
            Estos términos pueden actualizarse. La fecha de la última actualización figura al
            principio de esta página.
          </p>
        </section>
      </main>
    </div>
  );
}
