CREATE SCHEMA HAWKEYE;


-- -----------------------------------------------------
-- Table HAWKEYE.Study (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Study (
  study_id VARCHAR(20) NOT NULL,
  description VARCHAR(255),
  setup_time TIMESTAMP NOT NULL,
  PRIMARY KEY (study_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Role (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Role (
  role_name VARCHAR(20) NOT NULL,
  PRIMARY KEY (role_name)
);
-- Pre-defined Roles:
INSERT INTO HAWKEYE.Role
(role_name) VALUES
('salt'),
('buffer'),
('PEG')
;


-- -----------------------------------------------------
-- Table HAWKEYE.Ingredient (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Ingredient (
  ingredient_name VARCHAR(100) NOT NULL,
  cation VARCHAR(20),
  anion VARCHAR(20),
  formula VARCHAR(40),
  PRIMARY KEY (ingredient_name)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Cocktail (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Cocktail (
  cocktail_id VARCHAR(10) NOT NULL,
  commercial_code VARCHAR(255),
  refractive_index_temperature DOUBLE,
  refractive_index_value DOUBLE,
  PRIMARY KEY (cocktail_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Cocktail_Ingredient (Ingredient & Cocktail)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Cocktail_Ingredient (
  cocktail_id VARCHAR(10) NOT NULL,
  ingredient_name VARCHAR(100) NOT NULL,
  concentration_value DOUBLE,
  concentration_unit VARCHAR(10),
  ph DOUBLE,
  CONSTRAINT pk_cocktail_ingredient_assoc PRIMARY KEY (cocktail_id, ingredient_name),
  FOREIGN KEY (cocktail_id)
   REFERENCES HAWKEYE.Cocktail (cocktail_id),
  FOREIGN KEY (ingredient_name)
   REFERENCES HAWKEYE.Ingredient (ingredient_name)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Cocktail_Ingredient_Role (Cocktail_Ingredient & Role)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Cocktail_Ingredient_Role (
  cocktail_id VARCHAR(10) NOT NULL,
  ingredient_name VARCHAR(100) NOT NULL,
  role_name VARCHAR(20) NOT NULL,
  CONSTRAINT pk_cocktail_ingredient_role_assoc PRIMARY KEY (cocktail_id, ingredient_name, role_name),  
  CONSTRAINT fk_cocktail_ingredient FOREIGN KEY (cocktail_id, ingredient_name)
   REFERENCES HAWKEYE.Cocktail_Ingredient (cocktail_id, ingredient_name),
  FOREIGN KEY (role_name)
   REFERENCES HAWKEYE.Role (role_name)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Composite_List (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Composite_List (
  composite_list_id VARCHAR(20) NOT NULL,
  generation VARCHAR(12) NOT NULL,
  type VARCHAR(12) NOT NULL,
  creator VARCHAR(20) NOT NULL,
  notes VARCHAR(255),
  PRIMARY KEY (composite_list_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Cocktail_Listing (Cocktail & Composite_list)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Cocktail_Listing (
  composite_list_id VARCHAR(20) NOT NULL,
  cocktail_id VARCHAR(10) NOT NULL,
  CONSTRAINT pk_cocktail_listing_assoc PRIMARY KEY (composite_list_id,cocktail_id),
  FOREIGN KEY (composite_list_id)
   REFERENCES HAWKEYE.Composite_List (composite_list_id),
  FOREIGN KEY (cocktail_id)
   REFERENCES HAWKEYE.Cocktail (cocktail_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Plate (Composite_List & Study)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Plate (
  plate_id VARCHAR(10) NOT NULL,
  type VARCHAR(12) NOT NULL,
  temperature DOUBLE NOT NULL,
  setup_time TIMESTAMP NOT NULL,
  creator VARCHAR(20) NOT NULL,
  notes VARCHAR(255),
  composite_list_id VARCHAR(20) NOT NULL,
  study_id VARCHAR(20),
  PRIMARY KEY (plate_id),
  FOREIGN KEY (composite_list_id)
   REFERENCES HAWKEYE.Composite_List (composite_list_id),
  FOREIGN KEY (study_id)
   REFERENCES HAWKEYE.Study (study_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Sample (Plate)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Sample (
  sample_id VARCHAR(10) NOT NULL,
  sample_name VARCHAR(100),
  origin_plate_id VARCHAR(10) NOT NULL,
  concentration_value DOUBLE,
  concentration_unit VARCHAR(10),
  PRIMARY KEY (sample_id),
  FOREIGN KEY (origin_plate_id)
   REFERENCES HAWKEYE.Plate (plate_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Sample_Ingredient (Ingredient & Sample)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Sample_Ingredient (
  sample_id VARCHAR(10) NOT NULL,
  ingredient_name VARCHAR(100) NOT NULL,
  concentration_value DOUBLE,
  concentration_unit VARCHAR(10),
  ph DOUBLE,
  CONSTRAINT pk_sample_ingredient_assoc PRIMARY KEY (sample_id,ingredient_name),
  FOREIGN KEY (sample_id)
   REFERENCES HAWKEYE.Sample (sample_id),
  FOREIGN KEY (ingredient_name)
   REFERENCES HAWKEYE.Ingredient (ingredient_name)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Sample_Ingredient_Role (Sample_Ingredient & Role)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Sample_Ingredient_Role (
  sample_id VARCHAR(10) NOT NULL,
  ingredient_name VARCHAR(100) NOT NULL,
  role_name VARCHAR(20) NOT NULL,
  CONSTRAINT pk_sample_ingredient_role_assoc PRIMARY KEY (sample_id, ingredient_name, role_name),
  CONSTRAINT fk_sample_ingredient FOREIGN KEY (sample_id, ingredient_name)
   REFERENCES HAWKEYE.Sample_Ingredient (sample_id, ingredient_name),
  FOREIGN KEY (role_name)
   REFERENCES HAWKEYE.Role (role_name)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Experiment (Plate, Sample, Cocktail)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Experiment (
  experiment_id VARCHAR(15) GENERATED ALWAYS AS (plate_id||'.'||TRIM(CHAR(well_position))) NOT NULL,
  plate_id VARCHAR(10) NOT NULL,
  well_position SMALLINT NOT NULL,
  sample_id VARCHAR(10) NOT NULL,
  cocktail_id VARCHAR(10) NOT NULL,
  sample_volume DOUBLE NOT NULL,
  cocktail_volume DOUBLE NOT NULL,
  PRIMARY KEY (experiment_id),
  FOREIGN KEY (cocktail_id)
   REFERENCES HAWKEYE.Cocktail (cocktail_id),
  FOREIGN KEY (sample_id)
   REFERENCES HAWKEYE.Sample (sample_id),
  FOREIGN KEY (plate_id)
   REFERENCES HAWKEYE.Plate (plate_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Archive (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Archive (
  archive_name varchar(40) NOT NULL,
  PRIMARY KEY (archive_name)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Image_Profile (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Image_Profile (
  image_profile_id SMALLINT NOT NULL,
  image_profile_name VARCHAR(20) NOT NULL,
  width SMALLINT NOT NULL,
  height SMALLINT NOT NULL,
  bit_depth SMALLINT NOT NULL,
  PRIMARY KEY (image_profile_id)
);
-- Pre-defined Profiles:
INSERT INTO HAWKEYE.Image_Profile
(image_profile_id, image_profile_name, width, height, bit_depth) VALUES
(1, 'HTS Mono', 632, 504, 8),
(2, 'HTS Color', 768, 768, 24),
(3, 'Rockmaker Color', 1224, 1024, 24),
(4, 'Rockmaker Mono', 512, 512, 8)
;


-- -----------------------------------------------------
-- Table HAWKEYE.Image (Plate, Experiment)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Image (
  image_filename VARCHAR(40) NOT NULL,
  experiment_id VARCHAR(15) NOT NULL,
  image_profile_id SMALLINT NOT NULL,
  archive_name VARCHAR(40) NOT NULL,
  PRIMARY KEY (image_filename),
  FOREIGN KEY (image_profile_id)
   REFERENCES HAWKEYE.Image_Profile (image_profile_id),
  FOREIGN KEY (archive_name)
   REFERENCES HAWKEYE.Archive (archive_name),
  FOREIGN KEY (experiment_id)
   REFERENCES HAWKEYE.Experiment (experiment_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Score (none)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Score (
  score_id SMALLINT NOT NULL,
  score_name VARCHAR(40) NOT NULL,
  value_code SMALLINT NOT NULL,
  scheme VARCHAR(20) NOT NULL,
  hotkey VARCHAR(20) NOT NULL,
  icon_filename VARCHAR(20) NOT NULL,
  color_red SMALLINT NOT NULL,
  color_green SMALLINT NOT NULL,
  color_blue SMALLINT NOT NULL,
  PRIMARY KEY (score_id),
  CONSTRAINT uc_score_name UNIQUE (score_name, scheme),
  CONSTRAINT uc_score_value UNIQUE (value_code, scheme),
  CONSTRAINT uc_score_hotkey UNIQUE (hotkey, scheme),
  CONSTRAINT uc_score_icon UNIQUE (icon_filename, scheme),
  CONSTRAINT uc_score_color UNIQUE (color_red, color_green, color_blue, scheme)
);
-- Pre-defined scores:
INSERT INTO HAWKEYE.Score
(score_id, score_name, value_code, scheme, hotkey, icon_filename, color_red, color_green, color_blue) VALUES
(1, 'Unknown', 0, 'HWI 7-way', 'NUMPAD7', 'unknown.png', 220, 30, 240),
(2, 'Garbage', 5, 'HWI 7-way', 'NUMPAD6', 'garbage.png', 120, 20, 250),
(3, 'Clear', 10, 'HWI 7-way', 'NUMPAD1', 'clear.png', 190, 255, 255),
(4, 'Phase Separation', 25, 'HWI 7-way', 'NUMPAD2', 'phasesep.png', 58, 207, 200),
(5, 'Precipitate', 50, 'HWI 7-way', 'NUMPAD3', 'precip.png', 50, 220, 45),
(6, 'Skin', 60, 'HWI 7-way', 'NUMPAD4', 'skin.png', 240, 175, 26),
(7, 'Crystals', 90, 'HWI 7-way', 'NUMPAD5', 'crystals.png', 250, 50, 20)
;


-- -----------------------------------------------------
-- Table HAWKEYE.Image_Score (Image, Score)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Image_Score (
  image_filename VARCHAR(40) NOT NULL,
  score_id SMALLINT NOT NULL,
  CONSTRAINT pk_image_score_assoc PRIMARY KEY (image_filename,score_id),
  FOREIGN KEY (image_filename)
   REFERENCES HAWKEYE.Image (image_filename),
  FOREIGN KEY (score_id)
   REFERENCES HAWKEYE.Score (score_id)
);


-- -----------------------------------------------------
-- Table HAWKEYE.Experiment_Score (Experiment, Score)
-- -----------------------------------------------------
CREATE TABLE HAWKEYE.Experiment_Score (
  experiment_id VARCHAR(15) NOT NULL,
  score_id SMALLINT NOT NULL,
  CONSTRAINT pk_experiment_score_assoc PRIMARY KEY (experiment_id,score_id),
  FOREIGN KEY (experiment_id)
   REFERENCES HAWKEYE.Experiment (experiment_id),
  FOREIGN KEY (score_id)
   REFERENCES HAWKEYE.Score (score_id)
);