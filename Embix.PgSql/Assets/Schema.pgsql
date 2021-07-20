-- eix_token
CREATE TABLE public.eix_token (
	id serial NOT NULL,
	value varchar(100) NOT NULL,
	"language" varchar(5) NULL,
	CONSTRAINT token_pk PRIMARY KEY (id)
);
CREATE INDEX eix_token_value_idx ON public.eix_token (value);

-- eix_occurrence
CREATE TABLE public.eix_occurrence (
	id serial NOT NULL,
	token_id integer NOT NULL,
	field char(5) NOT NULL,
	target_id varchar(50) NOT NULL,
	CONSTRAINT eix_occurrence_pk PRIMARY KEY (id),
	CONSTRAINT eix_occurrence_fk FOREIGN KEY (token_id) REFERENCES public.eix_token(id) ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE INDEX eix_occurrence_field_idx ON public.eix_occurrence (field);
