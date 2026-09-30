START TRANSACTION;
ALTER TABLE identity."LoginMethods" ADD "ContactEmail" character varying(254);

CREATE TABLE identity."ReauthTickets" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Action" character varying(60) NOT NULL,
    "SourceMethodId" uuid,
    "TargetMethodId" uuid,
    "TokenHash" character varying(500) NOT NULL,
    "IssuedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "ConsumedAtUtc" timestamp with time zone,
    "ReturnUrl" character varying(4000),
    CONSTRAINT "PK_ReauthTickets" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ReauthTickets_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES identity."AspNetUsers" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_LoginMethods_UserId_Primary" ON identity."LoginMethods" ("UserId") WHERE "IsPrimary" = TRUE;

CREATE UNIQUE INDEX "IX_ReauthTickets_TokenHash" ON identity."ReauthTickets" ("TokenHash");

CREATE INDEX "IX_ReauthTickets_UserId_ExpiresAtUtc" ON identity."ReauthTickets" ("UserId", "ExpiresAtUtc");

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."ReauthTickets" TO mt_app;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260930133740_AccountManagement', '10.0.12');

COMMIT;

