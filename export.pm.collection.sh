#!/bin/bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"

# chmod u+x export.pm.collection0.sh
VER='0.0.9'

MODE='CORE'
UPDATE_CORE=1
UPDATE_SCREEN=0

VERSION="1.11.24"
S_VERSION="1.7.8"

# Define color variables
RED='\e[31m'
GREEN='\e[32m'
YELLOW='\e[33m'
BLUE='\e[34m'
DIM_GREY='\e[2;37m'
DIM_YELLOW='\e[2;33m'
DIM_GREEN='\e[2;32m'
BOLD='\e[1m'
NC='\e[0m' # No Color (Reset)

# Define script variables
ARG_HOST=$1

# Script paramenters variables
PARAM_COL_ID="2635491-7159668a-1a48-413a-a3bf-68e2e1a3a194"
PARAM_OUTPUT_JSON="1-inventory-reservations.postman_collection.json"

PARAM_PWD="-"



KEY_FILE="./.private.keys/postman.key"

while IFS= read -r line; do
    PASSWD="$line"
done < "$KEY_FILE"

pass_len=${#PASSWD}
printf " - key: ${RED}"


while [ "$pass_len" -ge 0 ]
do
    printf "#"
    pass_len=$((pass_len - 1))
done
printf "${NC}\n"
printf " - loging in ${DIM_GREY}...\n"
postman login --with-api-key $PASSWD
printf "${NC} - login: ${GREEN}completed${NC}\n"
printf " - exporting ${DIM_GREY}...\n"
postman collection get $PARAM_COL_ID --json > $PARAM_OUTPUT_JSON
printf "${NC} - export file ${BOLD}$PARAM_OUTPUT_JSON${NC}: ${GREEN}✔${NC}\n"