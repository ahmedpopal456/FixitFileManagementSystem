import re
import json
import fileinput
import sys
import argparse
import os


def replace_tokens(file_path:str, params: dict, token_regex:str = r'\"\{\{([\w]+)\}\}\"'):
    
    """
    Replaces tokens meeting specific regex requirements within a file, based on information
    found in a provided dictionary (e.g. {'device':'R00000012','id':'12'}: considering default regex, the token {{device}} would be replaced by its value)

    Parameters
    ----------
    file_path : str
        Path of the file to modify the content of
    params : dict
        Dictionary containing key/value pairs to be used in the replacing of tokens within a file
    token_regex : str
        Regex to be applied to the file to find all wanted tokens
    """
    with fileinput.FileInput(file_path,inplace=True) as file: 
        for line in file:

            result = line
            if re.search(token_regex,line):
                list_to_replace = re.findall(token_regex,line)
                for item in list_to_replace:
                    trimmed_item = item.replace('{','').replace('}','')
                    if trimmed_item in params.keys(): 
                        if isinstance(params[trimmed_item], str):
                            result = re.sub(token_regex,f'"{params[trimmed_item]}"',line)
                        elif isinstance(params[trimmed_item], bool):
                            token = str(params[trimmed_item])
                            result = re.sub(token_regex,token.lower(),line)
 
            sys.stdout.write(result)    
            

def json_string(argument:str):
    return json.dumps(argument)
                        
# Entry Point
#
if __name__ == "__main__":

    parser = argparse.ArgumentParser(description="Fixit FMS Deployment Tokenizer - App Settings")

    parser.add_argument("--filePath" , type=str, help="")
    parser.add_argument("--keyVaultUri" , type=str, help="")
    parser.add_argument("--keyvaultServicePrincipalId" , type=str, help="")
    parser.add_argument("--keyvaultServicePrincipalSecret" , type=str, help="")
    parser.add_argument("--appInsightsKey" , type=str, help="")
    parser.add_argument("--websiteBaseUrl" , type=str, help="")
    parser.add_argument("--empowerStorageName" , type=str, help="")
    parser.add_argument("--empowerStorageEndpoint" , type=str, help="")

    parser.add_argument("--clientId", type=str, help="")
    parser.add_argument("--clientSecret", type=str, help="")
    parser.add_argument("--mdmClientId", type=str, help="")
    parser.add_argument("--azureAdB2CInstance", type=str, help="")
    parser.add_argument("--azureAdB2CDomain", type=str, help="")
    parser.add_argument("--azureAdB2CTenantId", type=str, help="")
    parser.add_argument("--azureAdB2CExtensionAppId", type=str, help="")
    parser.add_argument("--azureAdB2CAudiences", type=str, help="")    
    parser.add_argument("--empowerEventGridOnImageUrlExpiredTopicKey", type=str, help="")    
    parser.add_argument("--empowerEventGridOnImageUrlExpiredTopicEndpoint", type=str, help="")    
    parser.add_argument("--empowerEventGridOnImageUrlsUpdateTopicKey", type=str, help="")    
    parser.add_argument("--empowerEventGridOnImageUrlsUpdateTopicEndpoint", type=str, help="")    


    try:
        # Parse Arguments 
        args = parser.parse_args().__dict__

        filePath = args['filePath']
        if not os.path.exists(filePath):
            raise Exception(f"FilePath environmental variable is not a valid path...")

        # Replace tokens with given token array 
        replace_tokens(filePath, args)
    
    except Exception as e:
        print(e)