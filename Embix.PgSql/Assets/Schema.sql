-- token
CREATE TABLE public."token" (
	id serial NOT NULL,
	value varchar(100) NOT NULL,
	"language" varchar(5) NULL,
	CONSTRAINT token_pk PRIMARY KEY (id)
);
CREATE INDEX token_value_idx ON public."token" (value);

-- occurrence
CREATE TABLE public.occurrence (
	id serial NOT NULL,
	tokenid integer NOT NULL,
	field char(5) NOT NULL,
	targetid varchar(50) NOT NULL,
	CONSTRAINT occurrence_pk PRIMARY KEY (id),
	CONSTRAINT occurrence_fk FOREIGN KEY (tokenid) REFERENCES public."token"(id) ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE INDEX occurrence_field_idx ON public.occurrence (field);
