import { NavLink, Outlet } from "react-router-dom";

const modules = [
  { path: "/designaciones", label: "Designaciones" },
  { path: "/aulas", label: "Aulas" },
  { path: "/portal", label: "Portal Docente" },
  { path: "/tareas", label: "Tareas" },
];

export default function App() {
  return (
    <>
      <header className="app-header">
        <h1>Ars Docendi</h1>
        <nav className="app-nav">
          {modules.map((m) => (
            <NavLink
              key={m.path}
              to={m.path}
              className={({ isActive }) => (isActive ? "active" : undefined)}
            >
              {m.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </>
  );
}
