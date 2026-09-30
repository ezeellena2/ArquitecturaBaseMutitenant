-- Recorrido manual local: ejecutar una sola vez en appdb de Development con la conexión propietaria.
-- Publica otra versión del texto vigente. No crea personas ni cambia aceptaciones existentes.
BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('manual:legal-publication', 0));
DO $$
DECLARE
    previous_id uuid;
    next_id uuid := gen_random_uuid();
    next_version integer;
BEGIN
    SELECT "Id" INTO previous_id FROM platform."LegalDocuments"
    WHERE "Kind" = 'Terms' AND "EffectiveAtUtc" <= CURRENT_TIMESTAMP
    ORDER BY "EffectiveAtUtc" DESC, "Version" DESC LIMIT 1;
    IF previous_id IS NULL THEN RAISE EXCEPTION 'No hay términos vigentes'; END IF;
    IF (SELECT count(*) FROM platform."LegalDocumentContents"
        WHERE "LegalDocumentId" = previous_id AND "Culture" IN ('es-AR', 'en-US')) <> 2
    THEN RAISE EXCEPTION 'Faltan los textos es/en'; END IF;
    SELECT max("Version") + 1 INTO next_version FROM platform."LegalDocuments" WHERE "Kind" = 'Terms';
    INSERT INTO platform."LegalDocuments" ("Id", "Kind", "Version", "EffectiveAtUtc")
    VALUES (next_id, 'Terms', next_version, CURRENT_TIMESTAMP);
    INSERT INTO platform."LegalDocumentContents" ("LegalDocumentId", "Culture", "Text")
    SELECT next_id, "Culture", "Text" FROM platform."LegalDocumentContents" WHERE "LegalDocumentId" = previous_id;
    RAISE NOTICE 'Publicada versión % de términos con los textos vigentes', next_version;
END $$;
COMMIT;
