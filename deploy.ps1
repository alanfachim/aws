 
(Get-ECRLoginCommand).Password | docker login --username AWS --password-stdin 469122751664.dkr.ecr.sa-east-1.amazonaws.com
docker build -t counter-image -f Dockerfile .
docker tag counter-image:latest 469122751664.dkr.ecr.sa-east-1.amazonaws.com/my-container:latest
docker push 469122751664.dkr.ecr.sa-east-1.amazonaws.com/my-container:latest 

#cria vpc nework
sam deploy --template-file VPC.yml  --stack-name vpcStack --capabilities CAPABILITY_IAM --region sa-east-1 --force-upload --parameter-overrides AvailabilityZone1=sa-east-1a AvailabilityZone2=sa-east-1b SingleNatGateway=true
#cria elastic cache redis
sam deploy --template-file Redis.yml  --stack-name CacheStak --capabilities CAPABILITY_IAM --region sa-east-1 --force-upload --parameter-overrides NetworkStackName=vpcStack ClusterName=cache CacheNodeType=cache.t2.micro CacheEngine=redis CacheNodeCount=1 AutoMinorVersionUpgrade=true

sam deploy --template-file Tamplate.yaml  --stack-name ECSStak12 --capabilities CAPABILITY_NAMED_IAM --region sa-east-1 --force-upload --parameter-overrides vpcStack=vpcStack cacheStack=CacheStak EnvironmentName=manga InstanceType=t3.small ClusterSize=3 ECSAMI=/aws/service/ecs/optimized-ami/amazon-linux/recommended/image_id

