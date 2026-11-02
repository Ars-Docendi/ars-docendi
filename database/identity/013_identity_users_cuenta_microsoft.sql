-- identity.users: la cuenta Microsoft se vincula en el primer ingreso.
--
-- `azure_oid` (objeto) y `azure_tid` (tenant) identifican la cuenta Microsoft de
-- forma inmutable; el mail es mutable y sólo sirve para encontrar al usuario la
-- primera vez. Ambos quedan vacíos hasta ese primer ingreso.
--
-- Hasta este cambio el alta generaba un `azure_oid` aleatorio y nunca hubo un
-- ingreso real, así que todos los valores existentes son provisionales y se vacían.

ALTER TABLE identity.users ALTER COLUMN azure_oid DROP NOT NULL;

ALTER TABLE identity.users ADD COLUMN azure_tid UUID NULL;

UPDATE identity.users SET azure_oid = NULL WHERE azure_oid IS NOT NULL;

ALTER TABLE identity.users
    ADD CONSTRAINT users_cuenta_microsoft_completa
    CHECK ((azure_oid IS NULL) = (azure_tid IS NULL));
